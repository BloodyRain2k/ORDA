using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class VisualHelper
	{
		const int numLines = 5;
		LineRenderer[] lines = new LineRenderer[numLines];
		Transform transform = null;
		float startWidth = 0.2f;
		float endWidth = 0.0f;

		public VisualHelper (Transform tf)
		{
			transform = tf;
			checkLines ();
		}

		public void updateTransform (Transform tf)
		{
			transform = tf;
		}

		public void setWidth (float start, float end)
		{
			startWidth = start;
			endWidth = end;
		}

		private void checkLines()
		{
			// not sure why they would become null, but they do sometimes when things go kerbal :/
			for (int i=0; i<numLines; i++) {
				if(lines[i] != null) continue;
				GameObject obj = new GameObject ("Line");

				lines [i] = obj.AddComponent< LineRenderer > ();
				lines [i].transform.parent = transform;
				lines [i].transform.localPosition = Vector3.zero;
				lines [i].transform.localEulerAngles = Vector3.zero;
				lines [i].useWorldSpace = false;

				lines [i].material = new Material (Shader.Find ("Particles/Additive"));
				lines [i].SetWidth (startWidth, endWidth); 
				lines [i].SetVertexCount (2);
				lines [i].SetPosition (0, Vector3.zero);
				lines [i].SetPosition (1, Vector3.zero);

				switch (i) {
				case 0:
					lines [i].SetColors (Color.red, Color.red);
					break;
				case 1:
					lines [i].SetColors (Color.green, Color.green);
					break;
				case 2:
					lines [i].SetColors (Color.blue, Color.blue);
					break;
				case 3:
					lines [i].SetColors (Color.gray, Color.gray);
					break;
				case 4:
					lines [i].SetColors (Color.white, Color.white);
					break;
				default:
					lines [i].SetColors (Color.white, Color.white);
					break;
				}
			}
		}

		public void showLineShip(int line, Vector3 ps)
		{
			if(line < 0 || line >= numLines) return;
			checkLines();
			lines [line].SetPosition (1, ps);
		}

		public void showLineInertial(int line, Vector3 pi)
		{
			if(line < 0 || line >= numLines) return;
			if(transform == null) return;
			checkLines();

			Vector3 ps = transform.InverseTransformDirection (pi);
			lines [line].SetPosition (1, ps);
		}

		public void hideLine(int line)
		{
			if(line < 0 || line >= numLines) return;
			lines [line].SetPosition (1, Vector3.zero);
		}

		public void hideLines()
		{
			for(int i=0; i<numLines; i++) {
				lines [i].SetPosition(1, Vector3.zero);
			}
		}
	}
}

