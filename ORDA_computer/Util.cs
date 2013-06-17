using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Diagnostics;

namespace ORDA
{
	[Serializable()]
	public class sVector3
	{
		public sVector3 (Vector3 v)
		{
			x = v.x;
			y = v.y;
			z = v.z;
		}

		public Vector3 toVector3 ()
		{
			return new Vector3(x, y, z);
		}

		public float x, y, z;
	}

    [Serializable()]
    public class sVector3d
    {
        public sVector3d(Vector3d v)
        {
            x = v.x;
            y = v.y;
            z = v.z;
        }

        public Vector3d toVector3()
        {
            return new Vector3d(x, y, z);
        }

        public double x, y, z;
    }

	public class VesselComparer : IComparer<Vessel>
	{
	    Vessel vessel;

		public VesselComparer (Vessel v)
		{
			vessel = v;
		}

	    public int Compare (Vessel a, Vessel b)
		{
			double d = (vessel.orbit.pos - a.orbit.pos).magnitude - (vessel.orbit.pos - b.orbit.pos).magnitude;
			if (d < 0)
				return -1;
			if (d > 0)
				return 1;
			return 0;
		}
	}

	public class Util
	{
		//
		// print to KSP's debug log
		//
		static void print (string s)
		{
			UnityEngine.Debug.Log(s);
		}

		//
		// stack trace
		//
		static public void stackTrace ()
		{
			print ("=== stack trace ===");

			StackTrace stackTrace = new StackTrace();
  			StackFrame[] stackFrames = stackTrace.GetFrames();

  			foreach (StackFrame stackFrame in stackFrames)
  			{
    			/*print(stackFrame.GetFileName() + ":" +
				      stackFrame.GetFileLineNumber() + ":" +
				      stackFrame.GetMethod().Name + " ");*/
				print ("# " + stackFrame.ToString());
  			}
			print ("===================");
		}

		//
		// string utils
		//
		static public string truncateString(string s, int max)
		{
			return (s.Length <= max) ? (s) : (s.Substring(0, max) + "..");
		}

		//
		// sort vessels by distance to own vessel
		//
		static public List<Vessel> getSortedVesselList (Vessel v)
		{
			List<Vessel> vesselList = new List<Vessel>(FlightGlobals.Vessels);
        	vesselList.Sort(new VesselComparer(v));
			return vesselList;
		}

		//
		// vector/scalar utils
		//
		static public Vector3d reorder (Vector3d v, int order)
		{
			Vector3 r = Vector3.zero;

			switch (order) {
			case 123: r = new Vector3d(v.x, v.y, v.z); break;
			case 132: r = new Vector3d(v.x, v.z, v.y); break;
			case 213: r = new Vector3d(v.y, v.x, v.z); break;
			case 231: r = new Vector3d(v.y, v.z, v.x); break;
			case 312: r = new Vector3d(v.z, v.x, v.y); break;
			case 321: r = new Vector3d(v.z, v.y, v.x); break;
			default:  r = new Vector3d(v.x, v.y, v.z); break;
			}

			return r;
		}

		static public float maxElement (Vector3 v)
		{
			float absx = Mathf.Abs(v.x);
			float absy = Mathf.Abs(v.y);
			float absz = Mathf.Abs(v.z);
			float max = absx;
			if(absy > max) max = absy;
			if(absz > max) max = absz;
			return max;
		}

		static public Vector3 absElements (Vector3 v)
		{
			return new Vector3(Mathf.Abs (v.x), Mathf.Abs (v.y), Mathf.Abs (v.z));
		}

		static public string formatValue (double value, string unit, string format="F1")
		{
			string modifier = "";

			double absValue = value;
			if (absValue < 0)
				absValue = -value;

			if (absValue > 1e9) {
				value /= 1e9;
				modifier = "G";
			} else if (absValue > 1e6) {
				value /= 1e6;
				modifier = "M";
			} else if (absValue > 1e3) {
				value /= 1e3;
				modifier = "k";
			} else if (absValue < 1e0) {
				value *= 1e3;
				modifier = "m";
			} else if (absValue < 1e-3) {
				value *= 1e6;
				modifier = "µ";
			}

			if (modifier == "G" || modifier == "M" || modifier == "k") {
				format = "F3";
			}

			string s = value.ToString (format) + modifier + unit;
			return s;
		}

		static public Vector3 shipToInertial(Vessel vessel, Vector3 s) // used to convert MoI & torque
		{
			Vector3 ix = new Vector3(1,0,0);
			Vector3 iy = new Vector3(0,1,0);
			Vector3 iz = new Vector3(0,0,1);
			Vector3 sx = vessel.ReferenceTransform.TransformDirection(ix);
			Vector3 sy = vessel.ReferenceTransform.TransformDirection(iy);
			Vector3 sz = vessel.ReferenceTransform.TransformDirection(iz);

			Vector3 i = Vector3.zero;
			i.x = Vector3.Dot(ix, sx) * s.x +
				  Vector3.Dot(ix, sy) * s.y + 
				  Vector3.Dot(ix, sz) * s.z;
			i.y = Vector3.Dot(iy, sx) * s.x +
				  Vector3.Dot(iy, sy) * s.y + 
				  Vector3.Dot(iy, sz) * s.z;
			i.z = Vector3.Dot(iz, sx) * s.x +
				  Vector3.Dot(iz, sy) * s.y + 
				  Vector3.Dot(iz, sz) * s.z;
			return i;
		}

		static public Vector3 inertialToShip(Vessel vessel, Vector3 i)
		{
			Vector3 sx = new Vector3(1,0,0);
			Vector3 sy = new Vector3(0,1,0);
			Vector3 sz = new Vector3(0,0,1);
			Vector3 ix = vessel.ReferenceTransform.InverseTransformDirection(sx);
			Vector3 iy = vessel.ReferenceTransform.InverseTransformDirection(sy);
			Vector3 iz = vessel.ReferenceTransform.InverseTransformDirection(sz);

			Vector3 s = Vector3.zero;
			s.x = (Vector3.Dot(sx, ix)) * i.x +
				  (Vector3.Dot(sx, iy)) * i.y + 
				  (Vector3.Dot(sx, iz)) * i.z;
			s.y = (Vector3.Dot(sy, ix)) * i.x +
				  (Vector3.Dot(sy, iy)) * i.y + 
				  (Vector3.Dot(sy, iz)) * i.z;
			s.z = (Vector3.Dot(sz, ix)) * i.x +
				  (Vector3.Dot(sz, iy)) * i.y + 
				  (Vector3.Dot(sz, iz)) * i.z;
			return s;
		}

		//
		// orbit utils (not working properly :/)
		//
		static public double trueAnomalyToEccAnomaly(double e, double f)
		{
			double E = Math.Atan( Math.Sqrt((1-e)/(1+e)) * Math.Tan(f/2) );
			if(E < 0.0) E += 3/2 * Math.PI;
			E *= 2.0;
			return E;
		}

		static public double calcMeanAnomaly(double e, double E)
		{
			return E - e * Math.Sin(E);	
		}

		static public double getInterceptTime (Vessel targetVessel, Vessel interceptingVessel, bool ApA)
		{
			// calculates time until interceptingVessel reaches targetVessel's ApA/PeA

			double targetNode = targetVessel.orbit.argumentOfPeriapsis;
			if (ApA) {
				if(targetNode > 180) {
					targetNode -= 180;
				} else {
					targetNode += 180;
				}
			}

			double P = interceptingVessel.orbit.period;			// period
			double e = interceptingVessel.orbit.eccentricity;	// eccentricity
			double te = interceptingVessel.orbit.ObT;			// time at epoch
			double AoP = interceptingVessel.orbit.argumentOfPeriapsis;

			double f = ((targetNode - AoP) / 180) * Math.PI;	// true anomaly to target node
			double E = trueAnomalyToEccAnomaly (e, f);			// eccentric anomaly
			double M = calcMeanAnomaly (e, E);					// mean anomaly
			double n = 2 * Math.PI / P;							// mean motion
			double t = M / n;									// time to true anomaly
			double ti = t - te;									// time to target node
			if(ti < 0) ti += P;

			return ti;
		}

		//
		// impact/landing simulation
		//
		static public bool simulateImpact (FlightData flightData,
		                                   out double outMinAltitude,
		                                   out float outTime,
                                           out double outVelocity)
		{
			float engineAccel = flightData.availableEngineAccUp * FlightInputHandler.state.mainThrottle;

			// state
			double simAltitue = flightData.altitudeAGL;
            double simMinAltitude = simAltitue;
			float simTime = 0;
            double simVelocity = flightData.verticalSpeed;
			float simStep = 0.1f;
			float simMaxTime = 1000;
			bool simAborted = false;

			// simple integrator
			// TODO (dm/dt)
			while (true) {
				double r = flightData.vessel.mainBody.Radius + simAltitue;
				float g = (float)(flightData.vessel.mainBody.gravParameter / (r * r));

				simVelocity += (engineAccel - g) * simStep;
				simAltitue += simVelocity * simStep;
				simTime += simStep;

				if (simAltitue < simMinAltitude) {
					simMinAltitude = simAltitue;
				}

				if (simTime > simMaxTime) {
					simAborted = true;
					break;
				}
				if (simAltitue < 0) {
					break;
				}
			}

			if (simAborted) {
				outMinAltitude = simMinAltitude;
				outTime = 0;
				outVelocity = 0;
				return false;
			} else {
				outMinAltitude = 0;
				outTime = simTime;
				outVelocity = simVelocity;
				return true;
			}
		}

		//
		// random number utils
		//
		static System.Random myRandom = new System.Random();
		static public float nextFloat()
		{
		    double val = myRandom.NextDouble(); // range 0.0 to 1.0
			return (float)val;
		}

		static public Vector3 nextVector3()
		{
			return new Vector3(nextFloat(), nextFloat(), nextFloat()).normalized;
		}

		//
		// fuel utils
		//
		static public void getVesselResource (Vessel vessel, string name, out float amount, out float maxAmount)
		{
			//float space = 0;
			amount = 0;
			maxAmount = 0;
			foreach (Part p in vessel.parts) {
				PartResource resource = p.Resources[name];
				if(resource != null) {
					//space += Mathf.Max(0, (float)(resource.maxAmount - resource.amount));
					amount += (float)resource.amount;
					maxAmount += (float)resource.maxAmount;
				}
			}
			//return space;
		}

		//
		// dock utils
		//
		static public bool isFreeDockingPort (Part p)
		{
			if (p == null) {
				return false;
			}
			foreach (PartModule pm in p.Modules) {
				if(pm is ModuleDockingNode) {
					ModuleDockingNode mdn = (ModuleDockingNode)pm;
					if(mdn.state == "Ready") {
						return true;
					}
				}
			}
			return false;
		}

		static public void setVesselTargetByPart(Part p)
		{
			foreach(PartModule pm in p.Modules) {
				if(pm is ModuleDockingNode && pm is ITargetable) {
					FlightGlobals.fetch.SetVesselTarget((ITargetable)pm);
				}
			}
		}
		
		static public void setVesselTarget(Vessel v)
		{
			FlightGlobals.fetch.SetVesselTarget((ITargetable)v);
		}

		static public void unsetVesselTarget()
		{
			FlightGlobals.fetch.SetVesselTarget(null);
		}

		static public void getDockRelPosAndAtt(Part local, Part target, out Vector3 relPosOut, out float distanceOut, out Vector3 eulerOut)
		{
			Vector3 localRingPosition = local.transform.position;
			Vector3 dockedRingPosition = target.transform.position;

			// get relative position & distance
			Vector3 dockPos = localRingPosition + local.transform.TransformDirection (new Vector3 (0, getDockPosY(local), 0));
			Vector3 relDockPos = dockedRingPosition - dockPos;
			float distance = relDockPos.magnitude;

			// get relative attitude & angle
			Quaternion relRotation1 = Quaternion.Inverse (target.transform.rotation) * local.transform.rotation;
			Vector3 euler1 = relRotation1.eulerAngles;
			euler1.z += 180;
			euler1 = eulerCenter(euler1);

			relPosOut = local.transform.InverseTransformDirection(relDockPos);
			distanceOut = distance;
			eulerOut = euler1; // pitch roll yaw
		}

		static public float getDockPosY(Part p)
		{
			return 0.0f;
		}

		static Vector3 eulerCenter (Vector3 e)
		{
			if (e.x > 180) {
				e.x -= 360;
			} else if (e.x < -180) {
				e.x += 360;
			}

			if (e.y > 180) {
				e.y -= 360;
			} else if (e.y < -180) {
				e.y += 360;
			}

			if (e.z > 180) {
				e.z -= 360;
			} else if (e.z < -180) {
				e.z += 360;
			}
			return e;
		}

		//
		// tutorial utils
		//
		static public ORDA_computer getActiveComputerFromVessel (Vessel vessel)
		{
			foreach (Part p in vessel.parts) {
				foreach(PartModule pm in p.Modules) {
					if(pm is ORDA_computer) {
						ORDA_computer computer = (ORDA_computer)pm;
						if(computer.isActiveSystem()) {
							return computer;
						}
					}
				}
			}
			return null;
		}
	}
}

