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
		public Vector3d orbitUp = Vector3.zero;				// inertial frame
		public Vector3d orbitVelocity = Vector3.zero;		// inertial frame
		public Vector3d orbitNormal = Vector3.zero;			// inertial frame
		public Vector3d targetRelPosition = Vector3.zero;	// inertial frame
		public Vector3d targetRelVelocity = Vector3.zero;	// inertial frame
		public Vector3d targetRelPositionShip = Vector3.zero;// ship frame
		public Vector3d targetRelVelocityShip = Vector3.zero;// ship frame
        public double altitudeASL = 0;
        public double altitudeAGL = 0;
		public double verticalSpeed = 0;
        public double horizontalSpeed = 0;

		// vehicle dynamics
		public float mass = 0;
		public Vector3 CoM = Vector3.zero;
		public Vector3 MoI = Vector3.zero;
        public Vector3 MoIRatio = Vector3.zero;

        public Vector3 MoI0 = Vector3.zero;
        public Vector3 MoI1 = Vector3.zero;
        public Vector3 MoI2 = Vector3.zero;
        public Vector3 MoI3 = Vector3.zero;

        public Vector3 availableTorque = Vector3.zero;		// cmg+(rcs if enabled) pitch, roll, yaw
		public Vector3 availableTorquePos = Vector3.zero;		// cmg+(rcs if enabled) pitch, roll, yaw
        public Vector3 availableTorqueNeg = Vector3.zero;		// cmg+(rcs if enabled) pitch, roll, yaw
        public Vector3 availableTorqueMax = Vector3.zero;	// cmg+rcs
		public Vector3 availableTorqueMaxPos = Vector3.zero;	// cmg+rcs
        public Vector3 availableTorqueMaxNeg = Vector3.zero;	// cmg+rcs
		public Vector3 availableForce = Vector3.zero;
		public Vector3 availableAngAcc = Vector3.zero;		// cmg+(rcs if enabled)
		public Vector3 availableAngAccMax = Vector3.zero;	// cmg+rcs
		public Vector3 availableLinAcc = Vector3.zero;
		public float availableEngineThrust = 0;
		public float availableEngineThrustUp = 0;
		public float availableEngineAcc = 0;
		public float availableEngineAccUp = 0;

        public float maxRCSDist = 0;

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
			orbitNormal = -Vector3d.Cross (orbitUp, orbitVelocity).normalized;
			if (targetVessel != null) {
				targetRelPosition = Util.reorder (targetVessel.orbit.pos - vessel.orbit.pos, 132);
				targetRelVelocity = Util.reorder (targetVessel.orbit.vel - vessel.orbit.vel, 132);
				targetRelPositionShip = vessel.ReferenceTransform.InverseTransformDirection (targetRelPosition);
				targetRelVelocityShip = vessel.ReferenceTransform.InverseTransformDirection (targetRelVelocity);
			} else {
				targetRelPosition = Vector3d.zero;
				targetRelVelocity = Vector3d.zero;
				targetRelPositionShip = Vector3d.zero;
				targetRelVelocityShip = Vector3d.zero;
			}
			altitudeASL = vessel.altitude;
			altitudeAGL = vessel.altitude - vessel.terrainAltitude;
			foreach (Part p in vessel.parts) {
				if (p.collider != null) {
					Vector3d bottomPoint = p.collider.ClosestPointOnBounds (vessel.mainBody.position);
					double partBottomAlt = vessel.mainBody.GetAltitude (bottomPoint) - vessel.terrainAltitude;
					altitudeAGL = Math.Max (0, Math.Min (altitudeAGL, partBottomAlt));
				}
			}
			Vector3d up = (CoM - vessel.mainBody.position).normalized;
			Vector3d velocityVesselSurface = vessel.orbit.GetVel () - vessel.mainBody.getRFrmVel (CoM);
			verticalSpeed = Vector3d.Dot (velocityVesselSurface, up);
			horizontalSpeed = (velocityVesselSurface - (up * verticalSpeed)).magnitude;

			// inspect vessel's parts
			// accumulate mass, inertia, torque and force
			mass = 0;
            // Easier to use the builtin than to compute it :p
            // But is it right ?
			//MoI = vessel.findLocalMOI (CoM);

            MoI = Vector3.zero;

            MoI0 = vessel.findLocalMOI(CoM);
            MoI1 = vessel.findLocalMOI(CoM);
            MoI2 = Vector3.zero;
            MoI3 = Vector3.zero;

            availableTorque = Vector3.zero;
			availableTorquePos = Vector3.zero;
            availableTorqueNeg = Vector3.zero;
            availableTorqueMax = Vector3.zero;
            availableTorqueMaxPos = Vector3.zero;
            availableTorqueMaxNeg = Vector3.zero;            
			availableForce = Vector3.zero;
			Vector3 availableForcePos = Vector3.zero;
			Vector3 availableForceNeg = Vector3.zero;
			availableEngineThrust = 0;
			availableEngineThrustUp = 0;

            maxRCSDist = 0;

			foreach (Part p in vessel.parts) {
                
                Vector3 partPosition = vessel.transform.InverseTransformDirection(p.Rigidbody.worldCenterOfMass - CoM);

                if (p.physicalSignificance == Part.PhysicalSignificance.FULL)
                {
                    float pmas = p.mass + p.GetResourceMass();
                    mass += pmas;

                    //MoI += p.Rigidbody.inertiaTensorRotation * p.Rigidbody.inertiaTensor;
                    MoI1 += p.Rigidbody.inertiaTensorRotation * p.Rigidbody.inertiaTensor;
                    MoI2 += p.Rigidbody.inertiaTensorRotation * p.Rigidbody.inertiaTensor;
                    //print(p.Rigidbody.inertiaTensorRotation.eulerAngles.ToString("F3"));

                    MoI3 += new Vector3(
                    (partPosition.y * partPosition.y + partPosition.z * partPosition.z) * pmas,
                    (partPosition.z * partPosition.z + partPosition.x * partPosition.x) * pmas,
                    (partPosition.x * partPosition.x + partPosition.y * partPosition.y) * pmas
                    );
                    
                    // Test show that that it's the right MoI 
                    MoI += new Vector3(
                    (partPosition.y * partPosition.y + partPosition.z * partPosition.z) * pmas,
                    (partPosition.z * partPosition.z + partPosition.x * partPosition.x) * pmas,
                    (partPosition.x * partPosition.x + partPosition.y * partPosition.y) * pmas
                    );
                    MoI += p.Rigidbody.inertiaTensorRotation * p.Rigidbody.inertiaTensor;

                }

                /*
                MoI += new Vector3(
                    (partPosition.y * partPosition.y + partPosition.z * partPosition.z) * pmas,
                    (partPosition.z * partPosition.z + partPosition.x * partPosition.x) * pmas,
                    (partPosition.x * partPosition.x + partPosition.y * partPosition.y) * pmas
                    );
                 */ 

				foreach(PartModule pm in p.Modules) {
					if (pm is ModuleRCS) {
						ModuleRCS moduleRcs = pm as ModuleRCS;
                        if (moduleRcs.isEnabled && !moduleRcs.isJustForShow)
                        {
							foreach(Transform t in moduleRcs.thrusterTransforms) {
								// get thruster attributes
                                Vector3 forcePosition = t.position - CoM;

                                maxRCSDist = Mathf.Max(maxRCSDist, forcePosition.magnitude);
                                                                                                                                               
                                Vector3 thrust = vessel.transform.InverseTransformDirection(t.up) * moduleRcs.thrusterPower;

                                Vector3 torqueVector = Vector3.Cross(partPosition, thrust);

								if (vessel.ActionGroups[KSPActionGroup.RCS]) {//FlightInputHandler.RCSLock == false) {
                                    availableTorquePos += new Vector3(torqueVector.x >= 0 ? torqueVector.x : 0 , torqueVector.y >= 0 ? torqueVector.y : 0, torqueVector.z >= 0 ? torqueVector.z : 0);
                                    availableTorqueNeg += new Vector3(torqueVector.x < 0 ? -torqueVector.x : 0 , torqueVector.y < 0 ? -torqueVector.y : 0, torqueVector.z < 0 ? -torqueVector.z : 0);
								}
                                availableTorqueMaxPos += new Vector3(torqueVector.x >= 0 ? torqueVector.x : 0, torqueVector.y >= 0 ? torqueVector.y : 0, torqueVector.z >= 0 ? torqueVector.z : 0);
                                availableTorqueMaxNeg += new Vector3(torqueVector.x < 0 ? -torqueVector.x : 0, torqueVector.y < 0 ? -torqueVector.y : 0, torqueVector.z < 0 ? -torqueVector.z : 0);

                                float fx = thrust.x;
                                float fy = thrust.y;
                                float fz = thrust.z;

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
                    availableTorquePos += torque;
                    availableTorqueNeg += torque;
					availableTorqueMaxPos += torque;
                    availableTorqueMaxNeg += torque;
				}
                 
				// liquid fuel engine
				else
                if (p is LiquidFuelEngine && p.State == PartStates.ACTIVE)
                {
					LiquidFuelEngine lfe = (LiquidFuelEngine)p;
					Vector3 tv = vessel.ReferenceTransform.TransformDirection(lfe.thrustVector);
					float dot = Vector3.Dot(up.normalized, tv.normalized);

					availableEngineThrust += lfe.maxThrust;
					availableEngineThrustUp += lfe.maxThrust * dot;
				}
			}

            MoIRatio = new Vector3(MoI0.x / MoI.x, MoI0.y / MoI.y, MoI0.z / MoI.z);                 

			availableForce.x = Mathf.Min (availableForceNeg.x, availableForcePos.x);
			availableForce.y = Mathf.Min (availableForceNeg.y, availableForcePos.y);
			availableForce.z = Mathf.Min (availableForceNeg.z, availableForcePos.z);

            availableTorque.x = Mathf.Min(availableTorqueNeg.x, availableTorquePos.x);
            availableTorque.y = Mathf.Min(availableTorqueNeg.y, availableTorquePos.y);
            availableTorque.z = Mathf.Min(availableTorqueNeg.z, availableTorquePos.z);

            availableTorqueMax.x = Mathf.Min(availableTorqueMaxNeg.x, availableTorqueMaxPos.x);
            availableTorqueMax.y = Mathf.Min(availableTorqueMaxNeg.y, availableTorqueMaxPos.y);
            availableTorqueMax.z = Mathf.Min(availableTorqueMaxNeg.z, availableTorqueMaxPos.z);

			// calculate available angular / linear acceleration based on physical properties
            availableAngAcc = new Vector3(availableTorque.x / MoI.x,
                                           availableTorque.y / MoI.y,
                                           availableTorque.z / MoI.z);

            availableAngAccMax = new Vector3(availableTorqueMax.x / MoI.x,
                                              availableTorqueMax.y / MoI.y,
                                              availableTorqueMax.z / MoI.z);

			availableLinAcc = new Vector3 (availableForce.x / mass,
			                               availableForce.y / mass,
			                               availableForce.z / mass);
			availableEngineAcc = availableEngineThrust / mass;
			availableEngineAccUp = availableEngineThrustUp / mass;

			valid = true;
		}

		void print (string s)
		{
			UnityEngine.Debug.Log ("FlightData: " + s);
		}
	}
}

