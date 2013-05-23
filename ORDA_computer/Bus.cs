using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class Bus
	{
		// user input
		public float yawReq = 0;
		public float pitchReq = 0;
		public float rollReq = 0;

		// gnc output
		public bool yprDriven = false;
		public bool yprRelative = false;
		public bool xyzDriven = false;
		public bool xyzRelative = false;
		public float yaw = 0;
		public float pitch = 0;
		public float roll = 0;
		public float x = 0;
		public float y = 0;
		public float z = 0;

		public Bus ()
		{
		}

		public void reset ()
		{
			yprDriven = xyzDriven = false;
			yprRelative = xyzRelative = true; // relative by default, disabled by gnc for eac
			yaw = pitch = roll = x = y = z = 0.0f;
		}

		public void clamp ()
		{
			if (!yprDriven) {
				yaw = pitch = roll = 0.0f;
			} else {
				yaw = Mathf.Clamp (yaw, -1.0f, +1.0f);
				pitch = Mathf.Clamp (pitch, -1.0f, +1.0f);
				roll = Mathf.Clamp (roll, -1.0f, +1.0f);
			}

			if (!xyzDriven) {
				x = y = z = 0.0f;
			} else {
				x = Mathf.Clamp(x, -1.0f, +1.0f);
				y = Mathf.Clamp(y, -1.0f, +1.0f);
				z = Mathf.Clamp(z, -1.0f, +1.0f);
			}
		}
	}
}

