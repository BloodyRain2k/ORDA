using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class FlightData
	{
		public bool valid = false;

		// our vessel & target (all set by caller)
		public Vessel vessel = null;
		public Vessel targetVessel = null;
		public Part vesselPart = null;
		public Part targetPart = null;
		public bool targetChanged = false;

		// vehicle state
		public Vector3 angularVelocity = Vector3.zero;		// ship frame
		public Vector3 orbitUp = Vector3.zero;				// inertial frame
		public Vector3 orbitVelocity = Vector3.zero;		// inertial frame
		public Vector3 orbitNormal = Vector3.zero;			// inertial frame
		public Vector3 targetRelPosition = Vector3.zero;	// inertial frame
		public Vector3 targetRelVelocity = Vector3.zero;	// inertial frame
		public Vector3 targetRelPositionShip = Vector3.zero;// ship frame
		public Vector3 targetRelVelocityShip = Vector3.zero;// ship frame
		public float altitudeASL = 0;
		public float altitudeAGL = 0;
		public float verticalSpeed = 0;
		public float horizontalSpeed = 0;

		// vehicle dynamics
		public float mass = 0;
		public Vector3 CoM = Vector3.zero;
		public Vector3 MoI = Vector3.zero;
		public Vector3 availableTorque = Vector3.zero;		// cmg+(rcs if enabled) pitch, roll, yaw
		public Vector3 availableTorqueMax = Vector3.zero;	// cmg+rcs
		public Vector3 availableForce = Vector3.zero;
		public Vector3 availableAngAcc = Vector3.zero;		// cmg+(rcs if enabled)
		public Vector3 availableAngAccMax = Vector3.zero;	// cmg+rcs
		public Vector3 availableLinAcc = Vector3.zero;
		public float availableEngineThrust = 0;
		public float availableEngineThrustUp = 0;
		public float availableEngineAcc = 0;
		public float availableEngineAccUp = 0;

		public FlightData ()
		{
			valid = false;
		}

		public void update (float dt)
		{
			// center of mass
			CoM = vessel.findWorldCenterOfMass ();

			// get vehicle state
			angularVelocity = vessel.ReferenceTransform.InverseTransformDirection (vessel.rigidbody.angularVelocity);
			orbitUp = Util.reorder (vessel.orbit.pos, 132).normalized;
			orbitVelocity = Util.reorder (vessel.orbit.vel, 132).normalized;
			orbitNormal = -Vector3.Cross (orbitUp, orbitVelocity).normalized;
			if (targetVessel != null) {
				targetRelPosition = Util.reorder (targetVessel.orbit.pos - vessel.orbit.pos, 132);
				targetRelVelocity = Util.reorder (targetVessel.orbit.vel - vessel.orbit.vel, 132);
				targetRelPositionShip = vessel.ReferenceTransform.InverseTransformDirection (targetRelPosition);
				targetRelVelocityShip = vessel.ReferenceTransform.InverseTransformDirection (targetRelVelocity);
			} else {
				targetRelPosition = Vector3.zero;
				targetRelVelocity = Vector3.zero;
				targetRelPositionShip = Vector3.zero;
				targetRelVelocityShip = Vector3.zero;
			}
			altitudeASL = (float)vessel.altitude;
			altitudeAGL = (float)(vessel.altitude - vessel.terrainAltitude);
			foreach (Part p in vessel.parts) {
				if (p.collider != null) {
					Vector3d bottomPoint = p.collider.ClosestPointOnBounds (vessel.mainBody.position);
					float partBottomAlt = (float)(vessel.mainBody.GetAltitude (bottomPoint) - vessel.terrainAltitude);
					altitudeAGL = Mathf.Max (0, Mathf.Min (altitudeAGL, partBottomAlt));
				}
			}
			Vector3 up = (CoM - vessel.mainBody.position).normalized;
			Vector3 velocityVesselSurface = vessel.orbit.GetVel () - vessel.mainBody.getRFrmVel (CoM);
			verticalSpeed = Vector3.Dot (velocityVesselSurface, up);
			horizontalSpeed = (velocityVesselSurface - (up * verticalSpeed)).magnitude;

			// inspect vessel's parts
			// accumulate mass, inertia, torque and force
			mass = 0;
			MoI = vessel.findLocalMOI (CoM);
			availableTorque = Vector3.zero;
			availableTorqueMax = Vector3.zero;
			availableForce = Vector3.zero;
			Vector3 availableForcePos = Vector3.zero;
			Vector3 availableForceNeg = Vector3.zero;
			availableEngineThrust = 0;
			availableEngineThrustUp = 0;

			foreach (Part p in vessel.parts) {
				mass += p.mass;
				MoI += p.Rigidbody.inertiaTensor;

				foreach(PartModule pm in p.Modules) {
					if (pm is ModuleRCS) {
						ModuleRCS moduleRcs = pm as ModuleRCS;
						if (moduleRcs.isEnabled) {
							foreach(Transform t in moduleRcs.thrusterTransforms) {
								// get thruster attributes
                                float forceMagnitude = moduleRcs.thrusterPower;
                                Vector3 forceVector = -t.up.normalized;
                                Vector3 forcePosition = t.position - CoM;

								// calculate how much torque this thruster might produce
								float CoM_TV_angle = Mathf.Acos(Vector3.Dot(forceVector, forcePosition.normalized));
								float torqueFraction = Mathf.Sin(CoM_TV_angle);
								float torque = forceMagnitude * forcePosition.magnitude * torqueFraction;

								// TODO figure out how to split into yaw, pitch & roll
								//      assume only xx% to compensate for now
								Vector3 torqueVector = (new Vector3 (torque, torque, torque)) * 0.10f;
								if (vessel.ActionGroups[KSPActionGroup.RCS]) {//FlightInputHandler.RCSLock == false) {
									availableTorque += torqueVector;
								}
								availableTorqueMax += torqueVector;

								// get components in ship frame
								Vector3 forceVectorShip = vessel.transform.InverseTransformDirection (forceVector).normalized;
								float fx = forceMagnitude * Vector3.Dot(forceVectorShip, new Vector3(1, 0, 0));
								float fy = forceMagnitude * Vector3.Dot(forceVectorShip, new Vector3(0, 1, 0));
								float fz = forceMagnitude * Vector3.Dot(forceVectorShip, new Vector3(0, 0, 1));

								if (fx < 0)
									availableForceNeg.x -= fx;
								else
									availableForcePos.x += fx;
								if (fy < 0)
									availableForceNeg.y -= fy;
								else
									availableForcePos.y += fy;
								if (fz < 0)
									availableForceNeg.z -= fz;
								else
									availableForcePos.z += fz;
							}
						}
					}
				}

				// stock command pod
				if (p is CommandPod) {
					float pyr = ((CommandPod)p).rotPower;
					Vector3 torque = new Vector3 (pyr, pyr, pyr);
					availableTorque += torque;
					availableTorqueMax += torque;
				}
				// liquid fuel engine
				else if (p is LiquidFuelEngine && p.State == PartStates.ACTIVE) {
					LiquidFuelEngine lfe = (LiquidFuelEngine)p;
					Vector3 tv = vessel.ReferenceTransform.TransformDirection(lfe.thrustVector);
					float dot = Vector3.Dot(up.normalized, tv.normalized);

					availableEngineThrust += lfe.maxThrust;
					availableEngineThrustUp += lfe.maxThrust * dot;
				}
			}
			availableForce.x = Mathf.Min (availableForceNeg.x, availableForcePos.x);
			availableForce.y = Mathf.Min (availableForceNeg.y, availableForcePos.y);
			availableForce.z = Mathf.Min (availableForceNeg.z, availableForcePos.z);

			// calculate available angular / linear acceleration based on physical properties
			availableAngAcc = new Vector3 (availableTorque.x / MoI.x,
			                               availableTorque.y / MoI.y,
			                               availableTorque.z / MoI.z);
			availableAngAccMax = new Vector3 (availableTorqueMax.x / MoI.x,
			                                  availableTorqueMax.y / MoI.y,
			                                  availableTorqueMax.z / MoI.z);
			availableLinAcc = new Vector3 (availableForce.x / mass,
			                               availableForce.y / mass,
			                               availableForce.z / mass);
			availableEngineAcc = availableEngineThrust / mass;
			availableEngineAccUp = availableEngineThrustUp / mass;

			// ...
			valid = true;
		}

		void print (string s)
		{
			UnityEngine.Debug.Log ("FlightData: " + s);
		}
	}
}

