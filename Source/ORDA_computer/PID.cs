using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class PID
	{
		float Kp = 10;
		float Ki = 0.01f;
		float Kd = 0;

		Vector3 integral = Vector3.zero;
		Vector3 prevError = Vector3.zero;
		Vector3 output = Vector3.zero;

		public PID ()
		{
		}

		public void reset ()
		{
			integral = Vector3.zero;
		}

		public void update(Vector3 error, float dt)
		{
			integral += error * dt;
			Vector3 derivate = (error - prevError) / dt;
			prevError = error;

			output = error * Kp + integral * Ki + derivate * Kd;
		}

		Vector3 getOutput()
		{
			return output;
		}
	}
}

