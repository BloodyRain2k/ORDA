using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class TeleporterLite
	{
		// will only work for short distances (ie. less than 50km)
		// will rip the ship into little pieces if the distance is much bigger
		/*static public void teleportToVessel (Vessel vessel, Vessel targetVessel, float d)
		{
			Vector3 j = Util.nextVector3 ().normalized * d;
			Vector3 targetPos = targetVessel.orbit.pos + j;
			Vector3 relPos = targetPos - vessel.orbit.pos;
			if (relPos.magnitude > 50000) {
				UnityEngine.Debug.Log ("TeleporterLite: warning: distance > 50km");
			}

			// teleport & match velocity
			if (vessel.packed == false) {
				UnityEngine.Debug.Log ("teleportToVessel unpacked " + relPos.magnitude.ToString ("F1"));

				vessel.Translate(Util.reorder (relPos, 132));
				vessel.GoOnRails ();
				vessel.orbit.vel = targetVessel.orbit.vel;
				vessel.GoOffRails ();
			} else {
				UnityEngine.Debug.Log ("teleportToVessel packed " + relPos.magnitude.ToString ("F1"));

				vessel.SetPosition(targetVessel.transform.position + j);
				vessel.orbit.vel = targetVessel.orbit.vel;
			}
		}*/

		static public void teleportToDockingPort (Vessel vessel, Part vesselPort, Part targetPort, float distance)
		{
			// check orientation
			Vector3 rp = Vector3.zero;
			Vector3 euler = Vector3.zero;
			float d = 0;
			Util.getDockRelPosAndAtt(vesselPort, targetPort, out rp, out d, out euler);
			float roll = euler.y;
			euler.y = 0;
			if (Util.maxElement (euler) > 5) {
				UnityEngine.Debug.Log ("TeleporterLite: incorrect orientation!");
				return;
			}

			Vector3 dockPos = vesselPort.transform.position + vesselPort.transform.TransformDirection (new Vector3 (0, Util.getDockPosY(targetPort) + 1 + distance, 0));
			Vector3 relDockPos = targetPort.transform.position - dockPos;
			if (relDockPos.magnitude > 500) {
				UnityEngine.Debug.Log ("TeleporterLite: distance > 500m");
				return;
			}

			// teleport
			vessel.Translate(relDockPos);

			// match velocity
			vessel.GoOnRails();
			vessel.orbit.vel = targetPort.vessel.orbit.vel;
			vessel.GoOffRails();
		}
	}

	public class Teleporter
	{
		FlightData flightData = null;

		enum Mode { IDLE=0, TELEPORT_TO_TARGET, TELEPORT_TO_ORBIT };
		Mode mode = Mode.IDLE;
		int step = 0;
		Vector3 subTargetPosition = Vector3.zero;

		public Teleporter (FlightData fd)
		{
			flightData = fd;
		}

		public void teleportToTarget ()
		{
			Vessel vessel = flightData.vessel;
			Vessel target = flightData.targetVessel;

			if (step != 0 || vessel == null || target == null) {
				print ("teleportToTarget: invalid configuration");
				return;
			}
			if (vessel.mainBody != target.mainBody) {
				print ("teleportToTarget: target must orbit same celestrial body");
				return;
			}
			mode = Mode.TELEPORT_TO_TARGET;
			step = 0;
		}

		public void teleportToOrbit ()
		{
			if (step != 0 || flightData.vessel == null) {
				print ("teleportToOrbit: invalid configuration");
				return;
			}
			if (flightData.vessel.mainBody.name == "Sun") {
				print ("teleportToOrbit: don't know what to do here");
				return;
			}

			mode = Mode.TELEPORT_TO_ORBIT;
			step = 0;
		}

		public void update (float dt)
		{
			Vessel vessel = flightData.vessel;
			if (vessel == null) {
				done ();
				return;
			}

			// teleport to target
			if (mode == Mode.TELEPORT_TO_TARGET) {
				Vessel targetVessel = flightData.targetVessel;

				// no target?
				if (targetVessel == null) {
					done ();
					return;
				}

				// get to 1'000'000m altitude
				else if (step == 0) {
					if (vessel.orbit.altitude < 1000000) {
						Vector3 p = vessel.orbit.pos.normalized;
						Vector3 t = Util.reorder (p, 132);
						vessel.Translate (t * 10000);
					} else {
						float w = Vector3.Angle(vessel.orbit.pos, targetVessel.orbit.pos);
						if(w > 90) {
							step = 1;
							subTargetPosition = Vector3.Cross(vessel.orbit.pos, targetVessel.orbit.pos).normalized * 1000000;
						} else {
							step = 2;
						}
					}
				}

				// don't crash into planet - teleport to sub position first
				else if(step == 1) {
					Vector3 r = (subTargetPosition - vessel.orbit.pos).normalized;
					Vector3 t = Util.reorder(r, 132);
					float d = 10000;

					vessel.Translate(t * d);

					if(r.magnitude < 20000) {
						step = 2;
					}
				}

				// get within 100m
				else if (step == 2) {
					float distance = (float)(vessel.orbit.pos - targetVessel.orbit.pos).magnitude;

					float d = 10000;
					if (distance < 500)
						d = 10;
					else if (distance < 5000)
						d = 100;
					else if (distance < 50000)
						d = 1000;

					Vector3 r = (targetVessel.orbit.pos - vessel.orbit.pos).normalized;
					Vector3 t = Util.reorder (r, 132);
					vessel.Translate (t * d);

					// start to match velocity?
					if (distance < 10000) {
						vessel.GoOnRails ();
						vessel.orbit.vel = targetVessel.orbit.vel;
						vessel.GoOffRails ();
					}

					// done?
					if (distance < 100) {
						done ();
					}
				}
			}
			// teleport to orbit
			else if (mode == Mode.TELEPORT_TO_ORBIT) {

				// get to 500km
				if (step == 0) {
					if (vessel.orbit.altitude < 500000) {
						Vector3 p = vessel.orbit.pos.normalized;
						Vector3 t = Util.reorder (p, 132);
						vessel.Translate (t * 10000);
					} else {
						step = 1;
					}
				}
				// set orbital velocity
				else if(step == 1) {
					vessel.GoOnRails();
					CelestialBody body = vessel.mainBody;
					double h = vessel.orbit.altitude;
					double v = Math.Sqrt( body.gravParameter / (body.Radius + h) );
					Vector3d p = vessel.orbit.pos.normalized;
					Vector3d n = new Vector3(0,0,-1);
					vessel.orbit.vel = Vector3d.Cross(p, n).normalized * v;
					vessel.GoOffRails();

					done ();
				}
			}
		}

		void done()
		{
			mode = Mode.IDLE;
			step = 0;
		}

		void print(string s)
		{
			UnityEngine.Debug.Log(s);
		}
	}
}

