using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class GNC
	{
		// settings
		public float Default_Kp_AngVel = 0.25f;
		public float Default_Kp_AngAcc = 2.0f;
		public float Default_Kp_Vel = 0.25f;
		public float Default_Kp_Acc = 2.0f;

		public const float Default_eacPulseLength = 0.1f;	// [s]
		public const float Default_eacPulseLevel = 1.0f;
		public const float Default_eacRate = 10.0f;			// [°/s]

		public const float dockAbortAttitude = 5.0f;		// [°]
		public const float dockAbortDeviation = 5.0f;		// [°]
		public const float dockAbortLatchMiss = 0.3f;		// [m]

		const float positionModeDistance = 50.0f;
		Vector3 dockEntryPoint = new Vector3(0, 50, 0);
		const float dockStateTransitionDelay = 2.5f;
		const float dockPosTransitionMargin = 1.0f;			// [m]
		const float dockPyrTransitionMargin = 2.5f;			// [°]

		float dockDistance = 50f;
		public float radius = 0;
		public float targetRadius = 0;
		
		// connectivity
		ORDA_computer parent = null;
		FlightData flightData = null;
		Bus bus = null;

		// states
		public enum Command { OFF=0, RATE, ATT, EAC, DOCK };
		public enum RateMode { IDLE=0, ZERO, ROLL, HOLD };
		public enum AttMode { IDLE=0, REF, HOLD, VP, VN, NP, NN, RP, RN, RPP, RPN, RVP, RVN };
		public enum PosMode { IDLE=0, ZERO, HOLD, VN, RN, RETREAT };
		public enum EACMode { IDLE=0, PULSE, RATE, RATE_ATT };
		public enum DockMode { IDLE=0, ATTITUDE, AUTO };
		public enum DockState { IDLE=0, ORIENT, ENTRY, APPROACH, DOCKED, DEPART, ABORT, EVADE };
		public enum DockAbort { UNKNOWN=0, ATTITUDE, DEVIATION, LATCH };

		Command command = Command.OFF;
		RateMode rateMode = RateMode.IDLE;
		AttMode attMode = AttMode.IDLE;
		EACMode eacMode = EACMode.IDLE;
		PosMode posMode = PosMode.IDLE;
		DockMode dockMode = DockMode.IDLE;
		DockState dockState = DockState.IDLE;
		DockAbort dockAbort = DockAbort.UNKNOWN;
		float dockStateTimer = 0;

		// hold settings
		bool userRateHoldRequest = false;
		bool userAttHoldRequest = false;
		bool userPosHoldRequest = false;
		Vector3 userRateSetting = Vector3.zero;
		Vector3 userAttSetting = Vector3.zero;
		Vector3 userAttUpSetting = Vector3.zero;
		Vector3 userPosSetting = Vector3.zero;

		// ang/lin controller settings
		float Kp_AngVel = 0;
		float Kp_AngAcc = 0;
		float Kp_Vel = 0;
		float Kp_Acc = 0;

		// eac settings & state
		float eacPulseLength = Default_eacPulseLength;
		float eacPulseLevel = Default_eacPulseLevel;
		float eacRate = Default_eacRate;
		Vector3 eacPrevYPR = Vector3.zero;
		Vector3 pulseTimes = Vector3.zero;
		bool eacAttInvalid = true;

		// dock settings
		float dockRollAdjust = 0;

		// network settings
		bool networkPropulsion = true;

		// controller inputs
		bool attActive = false;
		bool attUpActive = false;
		bool avelActive = false;
		bool rposActive = false; // hold relative position in inertial frame
		bool rvelActive = false; // hold relative velocity in ship frame
		bool rvelLimitMin = false;
		bool rvelLimitMax = false;

		Vector3 attCommand = Vector3.zero;
		Vector3 attUpCommand = Vector3.zero;
		Transform attTransform = null;
		Vector3 avelCommand = Vector3.zero;
		Vector3 aaccCommand = Vector3.zero;
		Vector3 rposCommand = Vector3.zero;
		Vector3 rposOffset = Vector3.zero;
		Transform rposTransform = null;
		Vector3 rvelCommand = Vector3.zero;
		float rvelLimit = 0;
		Vector3 accCommand = Vector3.zero;

		// controller outputs
		bool yprDriven = false;
		bool xyzDriven = false;
		Vector3 yprOut = Vector3.zero;
		Vector3 xyzOut = Vector3.zero;

		// controller status
		public Vector3 attError = Vector3.zero;
		public Vector3 pyrError = Vector3.zero;
		public Vector3 avelError = Vector3.zero;
		public Vector3 rposError = Vector3.zero;
		public Vector3 rvelError = Vector3.zero;
		public float dockDeviationAngle = 0;
		
		private LineRenderer line;

		//
		// public methods
		//
		public GNC (FlightData fd, Bus b, ORDA_computer p)
		{
			parent = p;
			flightData = fd;
			bus = b;
			
			Kp_AngVel = Default_Kp_AngVel = p.default_Kp_AngVel;
			Kp_AngAcc = Default_Kp_AngAcc = p.default_Kp_AngAcc;
			Kp_Vel = Default_Kp_Vel = p.default_Kp_Vel;
			Kp_Acc = Default_Kp_Acc = p.default_Kp_Acc;
			
//			createLine();
		}
		
		private LineRenderer createLine()
		{
			var obj = new GameObject("Line");
			var l = obj.AddComponent<LineRenderer>();
//			l.transform.parent = flightData.vessel.transform;
			l.transform.localPosition = Vector3.zero;
			l.transform.localEulerAngles = Vector3.zero;
			l.useWorldSpace = true;
			l.material = new Material (Shader.Find ("Particles/Additive"));
			l.SetWidth (0.1f, 0.1f); 
			l.SetVertexCount (2);
			l.SetPosition (0, Vector3.zero);
			return l;
		}

		public float getPowerFactor ()
		{
			// some random values
			float p = 0;
			switch (command) {
			case Command.OFF:
				p = 0.01f;
				break;
			case Command.RATE:
				p = (rateMode == RateMode.IDLE) ? (0.05f) : (0.24f);
				break;
			case Command.ATT:
				p = (attMode == AttMode.IDLE) ? (0.05f) : (0.32f);
				break;
			case Command.EAC:
				p = (eacMode == EACMode.IDLE) ? (0.05f) : (0.44f);
				break;
			case Command.DOCK:
				switch (dockMode) {
				case DockMode.IDLE:
					p = 0.05f;
					break;
				case DockMode.ATTITUDE:
					p = 0.75f;
					break;
				case DockMode.AUTO:
					p = 1.0f;
					break;
				}
				break;
			}
			if ((command == Command.RATE ||
			    command == Command.ATT ||
			    command == Command.EAC) &&
			    posMode != PosMode.IDLE) {
				p += 0.1f;
			}
			return p;
		}

		public void getStates (out Command outCommand, 
		                       out RateMode outRateMode, 
		                       out AttMode outAttMode,
		                       out EACMode outEacMode,
		                       out PosMode outPosMode,
		                       out DockMode outDockMode)
		{
			outCommand = command;
			outRateMode = rateMode;
			outAttMode = attMode;
			outEacMode = eacMode;
			outPosMode = posMode;
			outDockMode = dockMode;
		}
		
		public Command getCommand() {
			return command;
		}

		public void getDockState (out DockState outDockState,
		                          out DockAbort outDockAbort)
		{
			outDockState = dockState;
			outDockAbort = dockAbort;
		}

		public void getControllerSettings (out float outAngVel,
		                                   out float outAngAcc, 
		                                   out float outVel, 
		                                   out float outAcc)
		{
			outAngVel = Kp_AngVel;
			outAngAcc = Kp_AngAcc;
			outVel = Kp_Vel;
			outAcc = Kp_Acc;
		}

		public void setControllerSettings (float angVel,
		                                   float angAcc,
		                                   float vel,
		                                   float acc)
		{
			Kp_AngVel = angVel;
			Kp_AngAcc = angAcc;
			Kp_Vel = vel;
			Kp_Acc = acc;
		}

		public void getEACSettings (out float outPulseLength,
		                            out float outPulseLevel,
		                            out float outRate)
		{
			outPulseLength = eacPulseLength;
			outPulseLevel = eacPulseLevel;
			outRate = eacRate;
		}

		public void setEACSettings (float pulseLength,
		                            float pulseLevel,
		                            float rate)
		{
			eacPulseLength = pulseLength;
			eacPulseLevel = pulseLevel;
			eacRate = rate;
		}

		public void getDockSettings (out float outRollAdjust)
		{
			outRollAdjust = dockRollAdjust;
		}

		public void setDockSettings (float rollAdjust)
		{
			dockRollAdjust = rollAdjust;
		}

		public void getNetworkSettings (out bool outPropulsion)
		{
			outPropulsion = networkPropulsion;
		}

		public void setNetworkSettings (bool propulsion)
		{
			networkPropulsion = propulsion;
		}

		public void requestCommand (Command c)
		{
			command = c;

			rateMode = RateMode.IDLE;
			attMode = AttMode.IDLE;
			eacMode = EACMode.IDLE;
			dockMode = DockMode.IDLE;
			dockState = DockState.IDLE;

			if (command != Command.RATE && command != Command.ATT && command != Command.EAC)
				posMode = PosMode.IDLE;

			if (command == Command.DOCK)
			{
				if (flightData.targetVessel == null)
				{
					command = Command.OFF;
				}
				else
				{
					radius = getRadius(parent.vessel);
					targetRadius = getRadius(flightData.targetVessel);
				}
			}
		}

		public void requestRateMode (RateMode m)
		{
			if(command != Command.RATE)
				return;

			if(rateMode == m)
				rateMode = RateMode.IDLE;
			else
				rateMode = m;

			if(rateMode == RateMode.HOLD)
				userRateHoldRequest = true;
		}

		public void requestAttMode (AttMode m)
		{
			if(command != Command.ATT)
				return;

			if(attMode == m)
				attMode = AttMode.IDLE;
			else
				attMode = m;

			if(attMode == AttMode.HOLD)
				userAttHoldRequest = true;
		}

		public void requestEacMode (EACMode m)
		{
			if (command != Command.EAC)
				return;

			if (eacMode == m)
				eacMode = EACMode.IDLE;
			else
				eacMode = m;

			if (eacMode == EACMode.RATE_ATT)
				eacAttInvalid = true;
		}

		public void requestPosMode (PosMode m)
		{
			if(command == Command.DOCK)
				return;

			if(posMode == m)
				posMode = PosMode.IDLE;
			else
				posMode = m;

			if(posMode == PosMode.HOLD)
				userPosHoldRequest = true;
		}

		public void requestDockMode (DockMode m)
		{
			if (command != Command.DOCK)
				return;

			radius = getRadius(parent.vessel);
			targetRadius = getRadius(flightData.targetVessel);
			
			if (dockMode == m)
				dockMode = DockMode.IDLE;
			else
				dockMode = m;

			dockState = DockState.IDLE;
			dockAbort = DockAbort.UNKNOWN;

			// check dockRollAdjust and adjust if it does not match
			if (dockMode != DockMode.IDLE && flightData.targetPart != null) {
				/*float rollAngle = 0;
				bool restrictedRoll = flightData.targetPart.getRingRestrictions(out rollAngle);
				if(restrictedRoll && rollAngle != 0) {
					if((Mathf.Abs(dockRollAdjust) % rollAngle) > 1 || Mathf.Abs (dockRollAdjust) < 1) {
						dockRollAdjust = rollAngle;
						print ("adjusted dockRollAdjust to " + dockRollAdjust.ToString("F1"));
					}
				}*/
			}
		}

		public void requestDockEngage ()
		{
			if(command != Command.DOCK)
				return;

			if(dockMode != DockMode.AUTO)
				return;

			dockState = DockState.ORIENT;
			dockAbort = DockAbort.UNKNOWN;
			dockStateTimer = 0;
		}

		public void getConfiguration (out GNCconfig config)
		{
			config = new GNCconfig();

			config.command = command;
			config.rateMode = rateMode;
			config.attMode = attMode;
			config.eacMode = eacMode;
			config.posMode = posMode;
			config.dockMode = dockMode;
			config.dockState = dockState;
			config.dockAbort = dockAbort;
			config.userRateSetting = new sVector3(userRateSetting);
			config.userAttSetting = new sVector3(userAttSetting);
			config.userAttUpSetting = new sVector3(userAttUpSetting);
			config.userPosSetting = new sVector3(userPosSetting);
			config.Kp_AngVel = Kp_AngVel;
			config.Kp_AngAcc = Kp_AngAcc;
			config.Kp_Vel = Kp_Vel;
			config.Kp_Acc = Kp_Acc;
			config.eacPulseLength = eacPulseLength;
			config.eacPulseLevel = eacPulseLevel;
			config.eacRate = eacRate;
			config.dockRollAdjust = dockRollAdjust;
			config.networkPropulsion = networkPropulsion;
		}

		public void restoreConfiguration (GNCconfig config)
		{
			// should be good, might need to add some checks
			command = config.command;
			rateMode = config.rateMode;
			attMode = config.attMode;
			eacMode = config.eacMode;
			posMode = config.posMode;
			dockMode = config.dockMode;
			dockState = config.dockState;
			dockAbort = config.dockAbort;
			userRateSetting = config.userRateSetting.toVector3 ();
			userAttSetting = config.userAttSetting.toVector3 ();
			userAttUpSetting = config.userAttUpSetting.toVector3 ();
			userPosSetting = config.userPosSetting.toVector3 ();
			Kp_AngVel = config.Kp_AngVel;
			Kp_AngAcc = config.Kp_AngAcc;
			Kp_Vel = config.Kp_Vel;
			Kp_Acc = config.Kp_Acc;
			eacPulseLength = config.eacPulseLength;
			eacPulseLevel = config.eacPulseLevel;
			eacRate = config.eacRate;
			dockRollAdjust = config.dockRollAdjust;
			networkPropulsion = config.networkPropulsion;

			// ...
			if (command == Command.EAC && eacMode == EACMode.RATE_ATT) {
				eacAttInvalid = true;
			}
		}

		public void update (float dt)
		{
			Vessel vessel = flightData.vessel;

			// check states
			checkStates ();

			// reset commanding/stats
			attActive = false;
			attUpActive = false;
			avelActive = false;
			rposActive = false;
			rvelActive = false;
			rvelLimitMin = false;
			rvelLimitMax = false;

			attCommand = Vector3.zero;
			attUpCommand = Vector3.zero;
			attTransform = flightData.vessel.ReferenceTransform;
			avelCommand = Vector3.zero;
			aaccCommand = Vector3.zero;
			rposCommand = Vector3.zero;
			rposTransform = flightData.vessel.ReferenceTransform;
			rvelCommand = Vector3.zero;
			rvelLimit = 0;

			dockDeviationAngle = 0;

			// rate hold mastermode
			if (command == Command.RATE) {
				rateLogic ();
			}
			// attitude hold mastermode
			else if (command == Command.ATT) {
				attLogic ();
			}
			// enhanced attitude control
			else if (command == Command.EAC) {
				eacLogic ();
			}
			// docking mastermode
			else if (command == Command.DOCK) {
				dockLogic (dt);
			}

			// position modes
			if (command != Command.DOCK) {
				positionLogic ();
			}

			// ang/lin controller
			controller ();

			// local propulsion
			if (yprDriven) {
				bus.yprDriven = true;
				bus.yaw = yprOut.x;
				bus.pitch = yprOut.y;
				bus.roll = yprOut.z;
			}
			if (xyzDriven) {
				bus.xyzDriven = true;
				bus.x = xyzOut.x;
				bus.y = xyzOut.y;
				bus.z = xyzOut.z;
			}
		}

		//
		// private methods
		//
		private void checkStates ()
		{
			// docking override?
			if (command == Command.DOCK) {
				attMode = AttMode.IDLE;
				rateMode = RateMode.IDLE;
				posMode = PosMode.IDLE;
			}

			// no target vessel?
			if (flightData.targetVessel == null) {

				// disable invalid att hold modes
				if (command == Command.ATT &&
					(attMode == AttMode.RPP || attMode == AttMode.RPN ||
					attMode == AttMode.RVP || attMode == AttMode.RVN)) {
					requestAttMode (AttMode.IDLE);
				}
			}

			// no target vessel or target changed?
			if (flightData.targetVessel == null || flightData.targetChanged) {

				// disable position modes
				if (posMode != PosMode.IDLE) {
					requestPosMode (PosMode.IDLE);
				}
			}

			// no target vessel or docking ports?
			if (flightData.targetVessel == null || 
			    flightData.targetPart == null || 
			    flightData.vesselPart == null ||
			    flightData.targetChanged) {

				// disable dock master mode
				if(command == Command.DOCK)	{
					requestCommand(Command.OFF);
				}
			}
		}

		private void rateLogic ()
		{
			// process hold request
			if (userRateHoldRequest) {
				userRateHoldRequest = false;
				userRateSetting = flightData.angularVelocity;
			}

			avelActive = true;
			switch (rateMode) {
			case RateMode.ZERO:
				avelCommand = Vector3.zero;
				break;
			case RateMode.ROLL:
				avelCommand = new Vector3 (0, 0.5f, 0);
				break;
			case RateMode.HOLD:
				avelCommand = userRateSetting;
				break;
			default:
				avelActive = false;
				break;
			}

			float distance = rposError.magnitude;
			if (distance < 100) {
				rvelLimitMax = true;
				rvelLimit = 1.0f;
			}
		}

		private void attLogic ()
		{
			// process hold request
			if (userAttHoldRequest) {
				userAttHoldRequest = false;
				userAttSetting = flightData.vessel.ReferenceTransform.TransformDirection (new Vector3 (0, 1, 0));
				userAttUpSetting = flightData.vessel.ReferenceTransform.TransformDirection (new Vector3 (0, 0, 1));
			}

			attActive = true;
			switch (attMode) {
			case AttMode.REF:
				attUpActive = true;
				attCommand = new Vector3 (1, 0, 0);
				attUpCommand = new Vector3 (0, 1, 0);
				break;
			case AttMode.HOLD:
				attUpActive = true;
				attCommand = userAttSetting;
				attUpCommand = userAttUpSetting;
				break;
			case AttMode.VP:
				attCommand = flightData.orbitVelocity.normalized;
				break;
			case AttMode.VN:
				attCommand = -flightData.orbitVelocity.normalized;
				break;
			case AttMode.NP:
				attCommand = flightData.orbitNormal.normalized;
				break;
			case AttMode.NN:
				attCommand = -flightData.orbitNormal.normalized;
				break;
			case AttMode.RP:
				attCommand = flightData.orbitUp.normalized;
				break;
			case AttMode.RN:
				attCommand = -flightData.orbitUp.normalized;
				break;
			case AttMode.RPP:
				attCommand = flightData.targetRelPosition.normalized;
				break;
			case AttMode.RPN:
				attCommand = -flightData.targetRelPosition.normalized;
				break;
			case AttMode.RVP:
				attCommand = -flightData.targetRelVelocity.normalized;
				break;
			case AttMode.RVN:
				attCommand = flightData.targetRelVelocity.normalized;
				break;
			default:
				attActive = false;
				break;
			}
		}

		private void eacLogic ()
		{
			const float threshold = 0.1f;
			float y = (Mathf.Abs (bus.yawReq) > threshold) ? (Mathf.Sign (bus.yawReq)) : (0);
			float p = (Mathf.Abs (bus.pitchReq) > threshold) ? (Mathf.Sign (bus.pitchReq)) : (0);
			float r = (Mathf.Abs (bus.rollReq) > threshold) ? (Mathf.Sign (bus.rollReq)) : (0);
			float dt = Time.fixedDeltaTime;

			if (eacMode == EACMode.PULSE) {

				// look for transition
				if (Mathf.Abs (eacPrevYPR.x - y) > threshold / 2)
					pulseTimes.x = eacPulseLength;
				if (Mathf.Abs (eacPrevYPR.y - p) > threshold / 2)
					pulseTimes.y = eacPulseLength;
				if (Mathf.Abs (eacPrevYPR.z - r) > threshold / 2)
					pulseTimes.z = eacPulseLength;

				// drive bus
				bus.yprDriven = true;
				bus.yprRelative = false;
				if (pulseTimes.x > dt) {
					pulseTimes.x -= dt;
					bus.yaw = y * eacPulseLevel;
				}
				if (pulseTimes.y > dt) {
					pulseTimes.y -= dt;
					bus.pitch = p * eacPulseLevel;
				}
				if (pulseTimes.z > dt) {
					pulseTimes.z -= dt;
					bus.roll = r * eacPulseLevel;
				}

			} else if (eacMode == EACMode.RATE) {

				// command angular velocity
				bus.yprRelative = false;
				avelActive = true;
				avelCommand = new Vector3(-p, -r, -y) * (eacRate * Mathf.Deg2Rad);

			} else if (eacMode == EACMode.RATE_ATT) {

				// no input?
				if(y == 0 && p == 0 && r == 0) {

					// first time?
					if(Mathf.Abs (eacPrevYPR.x) > threshold/2 || 
					   Mathf.Abs (eacPrevYPR.y) > threshold/2 || 
					   Mathf.Abs (eacPrevYPR.z) > threshold/2 ||
					   eacAttInvalid) {
						eacAttInvalid = false;

						// store current orientation
						userAttSetting = flightData.vessel.ReferenceTransform.TransformDirection (new Vector3 (0, 1, 0));
						userAttUpSetting = flightData.vessel.ReferenceTransform.TransformDirection (new Vector3 (0, 0, 1));
					}

					// command attitude
					attActive = true;
					attUpActive = true;
					attCommand = userAttSetting;
					attUpCommand = userAttUpSetting;

				}
				// input on one or more axis
				else {
					// command angular velocity
					bus.yprRelative = false;
					avelActive = true;
					avelCommand = new Vector3(-p, -r, -y) * (eacRate * Mathf.Deg2Rad);
				}
			}

			eacPrevYPR = new Vector3(y, p, r);
		}

		private void positionLogic ()
		{
			// process hold request
			if (userPosHoldRequest) {
				userPosHoldRequest = false;
				userPosSetting = flightData.targetRelPosition;
			}

			if (posMode == PosMode.ZERO) {
				rvelActive = true;
				rvelCommand = Vector3.zero;
			} else if (posMode == PosMode.HOLD) {
				rposActive = true;
				rposCommand = userPosSetting;
			} else if (posMode == PosMode.VN) {
				rposActive = true;
				rposCommand = Util.reorder (flightData.targetVessel.orbit.vel, 132).normalized * positionModeDistance;
			} else if (posMode == PosMode.RN) {
				rposActive = true;
				rposCommand = Util.reorder (flightData.targetVessel.orbit.pos, 132).normalized * positionModeDistance;
			} else if (posMode == PosMode.RETREAT) {
				rvelActive = true;
				rvelCommand = flightData.targetRelPositionShip.normalized * 1.0f;
				if (flightData.targetRelPosition.magnitude > 50) {
					posMode = PosMode.ZERO;
				}
			}

			if (rposActive) {
				// limit to save some rcs fuel
				rvelLimitMax = true;
				float dist = rposError.magnitude;
				if (dist < 100) {
					rvelLimit = 1.0f;
				} else {
					rvelLimit = 2.5f;
				}
			}
		}


		private Quaternion getDockingPortRotation(Part dockPart)
		{
			if (dockPart.name == "dockingPortLateral") {
				return Quaternion.AngleAxis(-90, new Vector3(1, 0, 0));
			} else {
				return Quaternion.identity;
			}
		}

		private Quaternion getDockAttitude()
		{
			// rotate to account for the targets's docking port orientation
			Quaternion attitude = getDockingPortRotation(flightData.targetPart);

			// rotate by the desired clocking angle about the docking port forward vector
			attitude = attitude * Quaternion.AngleAxis(dockRollAdjust, new Vector3(0, 1, 0));

			// rotate the attitude 180 degrees about the docking port up vector
			// this negates the forward vector and will be the direction in which the ship will dock
			attitude = attitude * Quaternion.AngleAxis(180, new Vector3(0, 0, 1));

			// rotate to account for the vessel's docking port orientation
			attitude = attitude * Quaternion.Inverse(getDockingPortRotation(flightData.vesselPart));

			return attitude;
		}

		private float getDeviationAngle()
		{
			// get the target's dock rotation
			Quaternion targetDockRotation = getDockingPortRotation(flightData.targetPart);
			Vector3 vesselDockPosition = flightData.vesselPart.transform.position - flightData.vessel.ReferenceTransform.position;

			// get our current position relative to the target dock in the target dock frame
			Vector3 dockToVesselVec = flightData.vessel.ReferenceTransform.position - flightData.targetPart.transform.position + vesselDockPosition;
			dockToVesselVec = dockToVesselVec.normalized;

			// define a vector pointing out from the target's dock
			Vector3 dockVec = new Vector3(0, 1, 0);
			dockVec = targetDockRotation * dockVec;
			dockVec = flightData.targetPart.transform.TransformDirection(dockVec);

			// find the angle between the two vectors
			float deviationAngle = Mathf.Acos(Vector3.Dot(dockVec, dockToVesselVec)) * Mathf.Rad2Deg;

			if(float.IsNaN(deviationAngle)) return 0;
			return deviationAngle;
		}

		private void dockLogic (float dt)
		{
			// calculate the dock attitude based on the two docking ports
			Quaternion dockAttitude = getDockAttitude();

			// nominal (no rotation) dock attitude
			Vector3 forward = new Vector3(0, 1, 0);
			Vector3 up = new Vector3(0, 0, 1);

			// rotate the nominal vectors by the dock attitude quaternion
			forward = dockAttitude * forward;
			up = dockAttitude * up;

			// transform the vectors into the inertial frame
			forward = flightData.targetPart.transform.TransformDirection(forward);
			up = flightData.targetPart.transform.TransformDirection(up);

			// check to see if we are in the approach corridor
			dockDeviationAngle = getDeviationAngle();

			// maintain docking attitude
			if(dockMode == DockMode.ATTITUDE) {

				// command alignment
				attActive = true;
				attUpActive = true;
				attCommand = forward;
				attUpCommand = up;
				attTransform = flightData.vesselPart.transform;
			}
			// fully autonomous docking
			else if(dockMode == DockMode.AUTO) {

				// get the target's dock rotation
				Quaternion targetDockRotation = getDockingPortRotation(flightData.targetPart);

				// handle states
				if(dockState == DockState.ORIENT) {

					// command alignment
					attActive = true;
					attUpActive = true;
					attCommand = forward;
					attUpCommand = up;
					attTransform = flightData.vesselPart.transform;

					// next state?
					if(Util.maxElement(pyrError) < dockPyrTransitionMargin) {
						dockStateTimer += dt;
						if(dockStateTimer > dockStateTransitionDelay) {
							if (dockDeviationAngle < 100) {
								dockState = DockState.ENTRY;
								float dist = Vector3.Distance(flightData.vesselPart.transform.position, flightData.targetPart.transform.position);
								dockDistance = (dist < 50f ? dist : 50f);
							}
							else
							{
								dockState = DockState.EVADE;
							}
							dockStateTimer = 0;
						}
					} else {
						dockStateTimer = 0;
					}
				}
				else if(dockState == DockState.ENTRY || dockState == DockState.EVADE) {

					// move to entry
					// rotate the position command by the target dock rotation
					Vector3 evadeDir = (flightData.vesselPart.transform.position - flightData.targetPart.transform.position).normalized;
					evadeDir = evadeDir - (flightData.targetPart.transform.up * Vector3.Dot(evadeDir, flightData.targetPart.transform.up));
					evadeDir *= radius + targetRadius + 50f;
					
					rposActive = true;
					rposCommand = (dockState == DockState.ENTRY ? targetDockRotation * (dockEntryPoint / 50f) * dockDistance : evadeDir);
					rposTransform = flightData.targetPart.transform;
					rposOffset = flightData.vesselPart.transform.position - flightData.vessel.ReferenceTransform.position;

					// maintain alignment
					attActive = true;
					attUpActive = true;
					attCommand = forward;
					attUpCommand = up;
					attTransform = flightData.vesselPart.transform;

					// next state?
					if(Util.maxElement(rposError) < dockPosTransitionMargin && 
					   Util.maxElement(pyrError) < dockPyrTransitionMargin) {
						dockStateTimer += dt;
						if(dockStateTimer > dockStateTransitionDelay) {
							dockState = (dockState == DockState.ENTRY ? DockState.APPROACH : DockState.ENTRY);
							dockStateTimer = 0;
						}
					} else {
						dockStateTimer = 0;
					}
				}
				else if(dockState == DockState.APPROACH) {

					// approach
					rposActive = true;
					rposCommand = Vector3.zero;
					rposTransform = flightData.targetPart.transform;
					rposOffset = flightData.vesselPart.transform.position - flightData.vessel.ReferenceTransform.position;

					// maintain alignment
					attActive = true;
					attUpActive = true;
					attCommand = forward;
					attUpCommand = up;
					attTransform = flightData.vesselPart.transform;

					// abort?
					if(dockDeviationAngle > dockAbortDeviation) {
						dockState = DockState.ABORT;
						dockAbort = DockAbort.DEVIATION;
						dockStateTimer = 0;
					}
					if(Util.maxElement(pyrError) > dockAbortAttitude) {
						dockState = DockState.ABORT;
						dockAbort = DockAbort.ATTITUDE;
						dockStateTimer = 0;
					}
					/*if (relPos.y < -dockAbortLatchMiss) {
						//dockState = DockState.ABORT;
						//dockAbort = DockAbort.LATCH;
						//dockStateTimer = 0;
					}*/

					// limit approach velocity
					rvelLimitMax = true;
					float dist = rposError.magnitude;
					if(dist < 2.5f) {
						rvelLimitMin = true;
						rvelLimit = 0.1f;
					}
					else if(dist < 5.0f) {
						rvelLimit = 0.25f;
					}
					else if(dist < 10.0f) {
						rvelLimit = 0.5f;
					} else if(dist < 0.85f && rvelLimitMin) {
						requestCommand(Command.OFF);
					}
					else {
						rvelLimit = 1.0f;
					}
				}
				/*else if(dockState == DockState.DOCKED) {

					// undocked?
					if (((ORDA_dock)flightData.vesselPart).isDocked () == false) {
						dockState = DockState.DEPART;
						dockStateTimer = 0;
					}

					if(command != Command.OFF) {
						print ("dockLogic: command to OFF");
						requestCommand(Command.OFF);
					}
				}
				else if(dockState == DockState.DEPART) {

					// free drift a bit first
					if(distance > 2.5f) {

						// minimize rotation
						avelActive = true;
						avelCommand = Vector3.zero;

						// move to entry
						rposActive = true;
						rposDock = true;
						rposCommand = dockEntryPoint;
					}
				}*/
				else if(dockState == DockState.ABORT) {

					// minimize rotation
					avelActive = true;
					avelCommand = Vector3.zero;

					// move to entry
					rposActive = true;
					rposCommand = dockEntryPoint;
					rposTransform = flightData.targetPart.transform;
				}
				
//				if (line == null) { line = createLine(); }
//				
//				if (dockState == DockState.EVADE || dockState == DockState.ENTRY)
//				{
//					line.SetWidth(0.1f, 0.1f);
//					line.SetPosition(0, flightData.targetPart.transform.position);
//					line.SetPosition(1, flightData.targetPart.transform.position + rposCommand);
//				}
//				else
//				{
//					line.SetWidth(0f, 0f);
////					line.SetPosition(1, Vector3.zero);
//				}
			}
		}

		private Vector3 getPitchRollYawError()
		{
			Vector3 forwardError = attTransform.InverseTransformDirection(attCommand);
			Vector3 upError = attTransform.InverseTransformDirection(attUpCommand);

			// calculate get the pitch, roll, and yaw error in the attTransform frame
			float p = Mathf.Atan2 (forwardError.z, forwardError.y);
			float r = Mathf.Atan2 (upError.x, upError.z);
			float y = -Mathf.Atan2 (forwardError.x, forwardError.y);

			// convert pitch, roll, and yaw error from the attTransform frame to the vessel reference frame
			Vector3 pry = new Vector3(p, r, y);
			Vector3 pryInertial = attTransform.TransformDirection(pry);
			Vector3 pryVessel = flightData.vessel.ReferenceTransform.InverseTransformDirection(pryInertial);

			if (!attUpActive)
			{
				// cancel the roll error if up pointing is not active
				pryVessel[1] = 0;
			}

			return pryVessel;
		}

		private float getRadius (Vessel vessel)
		{
			Vector3d center = new Vector3d();
			float largestRadius = 0;
			
			foreach (Part p in vessel.Parts)
			{
				center += p.transform.position;
			}
			center /= vessel.Parts.Count;
			
			foreach (Part p in vessel.Parts)
			{
				float r = (float)Vector3d.Distance(center, p.transform.position) + p.collider.bounds.size.magnitude;
				if (largestRadius < r)
				{
					largestRadius = r;
				}
			}
			
			return largestRadius;
		}

		private void controller ()
		{
			yprDriven = false;
			yprOut = Vector3.zero;
			xyzDriven = false;
			xyzOut = Vector3.zero;

			attError = Vector3.zero;
			pyrError = Vector3.zero;
			avelError = Vector3.zero;
			rposError = Vector3.zero;
			rvelError = Vector3.zero;

			if (attActive && attTransform != null) {

				// get commanded direction in local axis
				Vector3 error = attTransform.InverseTransformDirection(attCommand);
				attError = error;
				
				// get the pitch, roll, yaw errors in the vessel frame
				Vector3 pyrErr = getPitchRollYawError();
				pyrError = pyrErr * Mathf.Rad2Deg;

				float p = pyrErr[0];
				float r = pyrErr[1];
				float y = pyrErr[2];

				// don't command rotations along the roll axis
				// if the pitch or yaw errors are too large
				if (Mathf.Abs(p) * Mathf.Rad2Deg > 10 ||
					Mathf.Abs(y) * Mathf.Rad2Deg > 10) {
					r = 0;
				}

				float ps = (p < 0) ? (-1) : (1);
				float rs = (r < 0) ? (-1) : (1);
				float ys = (y < 0) ? (-1) : (1);

				// w = sqrt( 2 * phi * (dw/dt) )
				avelActive = true;
				Vector3 angAcc = flightData.availableAngAcc;
				avelCommand.x = ps * Mathf.Sqrt (2 * Mathf.Abs (p) * angAcc.x) * Kp_AngVel;
				avelCommand.y = rs * Mathf.Sqrt (2 * Mathf.Abs (r) * angAcc.y) * Kp_AngVel;
				avelCommand.z = ys * Mathf.Sqrt (2 * Mathf.Abs (y) * angAcc.z) * Kp_AngVel;
			}

			if (avelActive) {
				Vector3 error = avelCommand - flightData.angularVelocity;
				avelError = error;
				aaccCommand = -error * Kp_AngAcc;

				// T = I * (dw/dt)
				float Tp = flightData.MoI.x * aaccCommand.x;
				float Tr = flightData.MoI.y * aaccCommand.y;
				float Ty = flightData.MoI.z * aaccCommand.z;

				yprDriven = true;
				yprOut.x = Mathf.Clamp (Ty / flightData.availableTorque.z, -1.0f, +1.0f);
				yprOut.y = Mathf.Clamp (Tp / flightData.availableTorque.x, -1.0f, +1.0f);
				yprOut.z = Mathf.Clamp (Tr / flightData.availableTorque.y, -1.0f, +1.0f);
			}

			if (rposActive && rposTransform != null) {
				Vector3 commandPos = rposTransform.TransformPoint(rposCommand);
				Vector3 vesselPos = flightData.vessel.ReferenceTransform.position;
				Vector3 error = vesselPos - commandPos + rposOffset;
				rposError = error;

				// transform the error into the vessel frame
				Vector3 shipFrameError = flightData.vessel.ReferenceTransform.InverseTransformDirection (error);
				float dx = shipFrameError.x;
				float dy = shipFrameError.y;
				float dz = shipFrameError.z;
				float absx = Mathf.Abs (dx);
				float absy = Mathf.Abs (dy);
				float absz = Mathf.Abs (dz);
				float sx = (dx > 0) ? (1) : (-1);
				float sy = (dy > 0) ? (1) : (-1);
				float sz = (dz > 0) ? (1) : (-1);

				// v = sqrt( 2 * error * a )
				rvelActive = true;
				Vector3 linAcc = flightData.availableLinAcc;
				rvelCommand.x = sx * Mathf.Sqrt (2 * absx * linAcc.x) * Kp_Vel;
				rvelCommand.y = sy * Mathf.Sqrt (2 * absy * linAcc.y) * Kp_Vel;
				rvelCommand.z = sz * Mathf.Sqrt (2 * absz * linAcc.z) * Kp_Vel;

				// limit?
				float mag = rvelCommand.magnitude;
				Vector3 n = rvelCommand.normalized;
				if ((rvelLimitMax && mag > rvelLimit) || (rvelLimitMin && mag < rvelLimit)) {
					rvelCommand = n * rvelLimit;
				}
			}

			if (rvelActive) {
				Vector3 error = rvelCommand - flightData.targetRelVelocityShip;
				rvelError = error;
				accCommand = error * Kp_Acc;

				// F = m * a
				float fx = flightData.mass * accCommand.x;
				float fy = flightData.mass * accCommand.y;
				float fz = flightData.mass * accCommand.z;

				xyzDriven = true;
				xyzOut.x = Mathf.Clamp (fx / flightData.availableForce.x, -1.0f, +1.0f);
				xyzOut.y = Mathf.Clamp (fy / flightData.availableForce.y, -1.0f, +1.0f);
				xyzOut.z = Mathf.Clamp (fz / flightData.availableForce.z, -1.0f, +1.0f);
			}
		}
		
		void print(string s)
		{
			UnityEngine.Debug.Log("GNC: " + s);
		}
	}

	[Serializable()]
	public class GNCconfig
	{
		public GNC.Command command;
		public GNC.RateMode rateMode;
		public GNC.AttMode attMode;
		public GNC.EACMode eacMode;
		public GNC.PosMode posMode;
		public GNC.DockMode dockMode;
		public GNC.DockState dockState;
		public GNC.DockAbort dockAbort;
		public sVector3 userRateSetting;
		public sVector3 userAttSetting;
		public sVector3 userAttUpSetting;
		public sVector3 userPosSetting;
		public float Kp_AngVel;
		public float Kp_AngAcc;
		public float Kp_Vel;
		public float Kp_Acc;
		public float eacPulseLength;
		public float eacPulseLevel;
		public float eacRate;
		public float dockRollAdjust;
		public bool networkPropulsion;
	}
}
