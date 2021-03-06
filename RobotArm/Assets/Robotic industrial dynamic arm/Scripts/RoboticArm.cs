﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/**
 * This script allows an agent running on the Q-Cog agent module 
 * or a human with keyboard/mouse to control the robotic arm
 */
public class RoboticArm : MonoBehaviour {

    // These variables are used as IDs for messages
    // They indicate how the data in the message should be interpreted
	public const int QUIT = 0;
	public const int STATE = 1;
	public const int ACTION = 2;
	public const int REWARD = 3;

	// The parts of the robotic arm
	public GameObject target;
	public Transform part0;
	public Transform part1;
	public Transform part2;
	public Transform part3;
	public Transform gripLeft;
	public Transform gripRight;
	private TCPServer server;

	public int tick;                   // Within the current iteration, how many updates have occurred
	public bool next_iter;             // Is it time for the next iteration
	private int terminal;              // Sent with the reward (0 = not terminal, 1 = terminal)
	private const int TICK_MAX = 300;  // Max number of update() loops in an iteration 

	void Start () {
		terminal = 0;
		tick = 0;
		next_iter = false;
		Application.runInBackground = true;  // Leave this on
		server = GameObject.FindObjectOfType<TCPServer>();
	}
	
	// Update is called once per frame
	void Update () {
		//HandleInput();  // Keyboard control for debugging the arm's movement/angles
		tick++;  // Update the count of updates that have occurred
		if (tick > TICK_MAX || terminal == 1 || next_iter == true) {
			next_iter = true;
			return;
		}
		TCPMessage state = GetState();                 // Get the state
		server.SendMessage (state);                    // Send the state
		TCPMessage action = server.BlockingReceive();  // Wait for the chosen action from the agent
		TCPMessage reward = Execute(action);           // Execute the action and receive reward
		server.SendMessage (reward);                   // Send reward to agent
	}


	// Returns a Message representing the state of the arm
	public TCPMessage GetState() {
		float jointAngle0 = part0.rotation.eulerAngles.y;
		float jointAngle1 = part1.rotation.eulerAngles.z;
		float jointAngle2 = part2.rotation.eulerAngles.z;
		float jointAngle3 = part3.rotation.eulerAngles.x;
		//float jointAngle4 = gripLeft.transform.localRotation.eulerAngles.z;
		Vector3 gripPos = gripLeft.position;
		float gripX = gripPos.x;
		float gripY = gripPos.y;
		float gripZ = gripPos.z;
		float targetX = target.transform.position.x;
		float targetY = target.transform.position.y;
		float targetZ = target.transform.position.z;

		TCPMessage state = new TCPMessage (STATE);
		state.AddData (jointAngle0 + "");
		state.AddData (jointAngle1 + "");
		state.AddData (jointAngle2 + "");
		state.AddData (jointAngle3 + "");
		//state.AddData (jointAngle4 + "");
		state.AddData (gripX + "");
		state.AddData (gripY + "");
		state.AddData (gripZ + "");
		state.AddData (targetX + "");
		state.AddData (targetY + "");
		state.AddData (targetZ + "");
		return state;
	}

    public TCPMessage Execute(TCPMessage action) {
		if (action.id != ACTION) {
			print ("ERROR: Expected message.id=" + ACTION + " but got " + action.id);
			return null;
		}

		int joint = Int32.Parse (action.getData () [0]);
		int degrees = Int32.Parse (action.getData () [1]);
		if (joint == 0) rotatePart0 (degrees);
		else if (joint == 1) rotatePart1 (degrees);
		else if (joint == 2) rotatePart2 (degrees);
		else if (joint == 3) rotatePart3 (degrees);
		else if (joint == 4) grip (degrees);

		return Reward ();
	}

	public TCPMessage Reward() {
		float distToTarget = (target.transform.position - gripLeft.transform.position).magnitude; // Distance from claw to target
		float r = -distToTarget;  // Reward amount

		if (distToTarget < 1.0f) {
			r += 100;      // Extra reward for reaching target
			terminal = 1;  // Target reached, so terminal state
		} else if (tick == TICK_MAX) {
			terminal = 1;  // Training iteration over, so terminal state
		}

		TCPMessage reward = new TCPMessage(REWARD);
		reward.AddData (r + "");
		reward.AddData (terminal + "");
		return reward;
	}

	void FixedUpdate () {
	}

	/** For debugging: allows arm to be controlled by mouse and keyboard
	 * To use: comment everything in the update loop except this method
	 * and disable the Experiment and Server Game Objects in the editor
	 */

	void HandleInput() {
		float t = Time.deltaTime; // Used to make rotation speed frame-rate independent
		float horizontal = Input.GetAxis("Horizontal");
		float vertical   = Input.GetAxis ("Vertical");
		float mouseX     = Input.GetAxis("Mouse X");
		float mouseY     = Input.GetAxis("Mouse Y");
		bool leftClick   = Input.GetMouseButton(0);
		bool rightClick  = Input.GetMouseButton(1);

		if (horizontal != 0) {
			rotatePart0 (t * 50 * horizontal);
		}
		if (vertical != 0) {
			rotatePart1 (t * 50 * vertical);
		}
		if (mouseX != 0) {
			rotatePart2 (t * 80 * mouseX);
		}
		if (mouseY != 0) {
			rotatePart3 (t * 500 * -mouseY);
		}
		if (leftClick) {
			grip (t * 100);
		}
		if (rightClick) {
			grip (-t * 100);
		}
	}

	// Rotate part by val degrees
	public void rotatePart0(float val) {
		part0.Rotate(0f, 0f, val);
	}

	// Rotate part by val degrees
	public void rotatePart1(float val) {
		Quaternion currentRot = new Quaternion (part1.localRotation.x, part1.localRotation.y, part1.localRotation.z, part1.localRotation.w);
		part1.Rotate(0f, 0f, val);
		float ang = Math.Abs(part1.localRotation.eulerAngles.z);
		if ((ang < 240 || ang > 360))
			part1.localRotation = currentRot;
	}

	// Rotate part by val degrees
	public void rotatePart2(float val) {
		Quaternion currentRot = new Quaternion (part2.localRotation.x, part2.localRotation.y, part2.localRotation.z, part2.localRotation.w);
		part2.Rotate(0f, 0f, val);
		float ang = Math.Abs(part2.localRotation.eulerAngles.z);
		if (ang < 0 || ang > 90)
			part2.localRotation = currentRot;
	}

	// Rotate part by val degrees
	public void rotatePart3(float val) {
		Quaternion currentRot = new Quaternion (part3.localRotation.x, part3.localRotation.y, part3.localRotation.z, part3.localRotation.w);
		part3.Rotate(val, 0f, 0f);
		float ang = Math.Abs(part3.localRotation.eulerAngles.x);
		if (ang < 20)
			part3.localRotation = currentRot;
	}

	// Close/open grip by val degrees
	public void grip(float val) {
		Quaternion currentRotLeft = new Quaternion (gripLeft.localRotation.x, gripLeft.localRotation.y, gripLeft.localRotation.z, gripLeft.localRotation.w);
		Quaternion currentRotRight= new Quaternion (gripRight.localRotation.x, gripRight.localRotation.y, gripRight.localRotation.z, gripRight.localRotation.w);
		gripLeft.Rotate (0f, 0f, val);
		gripRight.Rotate (0f, 0f, val);
		float ang = Math.Abs(gripLeft.localRotation.eulerAngles.y);
		if (ang > 30) {
			gripLeft.localRotation = currentRotLeft;
			gripRight.localRotation = currentRotRight;
		}
	}

}