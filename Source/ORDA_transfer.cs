using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine; 

namespace ORDA
{
	public class ORDA_transfer : PartModule
	{
		// cfg settings
		[KSPField]
		public string grappleKey;
		[KSPField]
		public float maxGrappleDistance;
		[KSPField]
		public float maxLineLength;

		// resources
		string[] knownResources = { "LiquidFuel",
									"Oxidizer",
									"MonoPropellant",
									"ElectricCharge" };

		// gui
		static int nextWindowId = windowsIDs.transfer;
		int windowId = -1;
		Rect windowPositionAndSize = new Rect();
		bool windowPositionInvalid = true;
		bool windowSizeInvalid = true;
		const int windowWidth = 200;

		// state
		enum LineState { IDLE=0, GRAPPLED, CONNECTED };
		LineState lineState = LineState.IDLE;
		Vessel grappledKerbal = null;
		Vessel connectedShip = null;
		Part connectedPart = null;
		bool transferToFlag = false;
		bool transferFromFlag = false;

		static List<Vessel> grappledKerbalsList = new List<Vessel>();

		int selectedResource = 0;
		int selectedRate = 0;
		float currentFlow = 0;
		float currentAmount = 0;
		float currentMaxAmount = 0;

		// visualization
		LineRenderer lineRenderer = new LineRenderer();

		private void windowGUI (int windowID)
		{
			GUIStyle style = new GUIStyle (GUI.skin.button); 
			style.normal.textColor = style.focused.textColor = Color.white;
			style.hover.textColor = style.active.textColor = Color.yellow;
			style.onNormal.textColor = style.onFocused.textColor = style.onHover.textColor = style.onActive.textColor = Color.green;
			style.padding = new RectOffset (4, 4, 4, 4);

			GUIStyle activeStyle = new GUIStyle (GUI.skin.button); 
			activeStyle.normal.textColor = activeStyle.focused.textColor = Color.red;
			activeStyle.hover.textColor = activeStyle.active.textColor = Color.yellow;
			activeStyle.onNormal.textColor = activeStyle.onFocused.textColor = activeStyle.onHover.textColor = activeStyle.onActive.textColor = Color.green;
			activeStyle.padding = new RectOffset (4, 4, 4, 4);

			GUILayout.BeginVertical ();

			GUILayout.BeginHorizontal();

			if (GUILayout.Button ("To", (transferToFlag) ? (activeStyle) : (style))) {
				transferToFlag = !transferToFlag;
				transferFromFlag = false;
			}
			if (GUILayout.Button ("From", (transferFromFlag) ? (activeStyle) : (style))) {
				transferFromFlag = !transferFromFlag;
				transferToFlag = false;
			}

			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal ();

			if (GUILayout.Button ("1/s", (selectedRate==0)?(activeStyle):(style), GUILayout.ExpandWidth (true))) {
				selectedRate = 0;
			}
			if (GUILayout.Button ("10/s", (selectedRate==1)?(activeStyle):(style), GUILayout.ExpandWidth (true))) {
				selectedRate = 1;
			}
			if (GUILayout.Button ("100/s", (selectedRate==2)?(activeStyle):(style), GUILayout.ExpandWidth (true))) {
				selectedRate = 2;
			}

			GUILayout.EndHorizontal ();

			for (int i=0; i<knownResources.Length; i++) {
				if(GUILayout.Toggle (i==selectedResource, knownResources[i], GUILayout.ExpandWidth (true))) {
					if(selectedResource != i) {
						transferToFlag = false;
						transferFromFlag = false;
					}
					selectedResource = i;
				}
			}

			GUILayout.Label("Rate: " + currentFlow.ToString("F1") + "/s");
			GUILayout.Label("Amount: " + currentAmount.ToString("F1") + "/" + currentMaxAmount.ToString("F1"));

			GUILayout.EndVertical();

			GUI.DragWindow();
		}

		private void drawGUI ()
		{
			// allocate window id
			if (windowId < 0) {
				windowId = nextWindowId++;
			}

			// hide window?
			if(FlightGlobals.ActiveVessel != this.vessel || !part.isControllable) return;
			if(lineState != LineState.CONNECTED) return;

			// position / size indicated invalid?
			if (windowPositionInvalid) {
				windowPositionInvalid = false;
				windowPositionAndSize.x = 0;
				windowPositionAndSize.y = 100;
			}
			if(windowSizeInvalid) {
				windowSizeInvalid = false;
				windowPositionAndSize.width = 10;
				windowPositionAndSize.height = 10;
			}

			// show window
			GUI.skin = HighLogic.Skin;
			windowPositionAndSize = GUILayout.Window (windowId, windowPositionAndSize, windowGUI, "Transfer", GUILayout.MinWidth (windowWidth));	 
		}

		public override void OnAwake()
		{
		}

		public override void OnStart(StartState state)
		{
			part.force_activate();

			RenderingManager.AddToPostDrawQueue(0, new Callback(drawGUI));

			GameObject obj = new GameObject ("Line");
			lineRenderer = obj.AddComponent< LineRenderer > ();
			lineRenderer.transform.parent = transform;
			lineRenderer.transform.localPosition = Vector3.zero;
			lineRenderer.transform.localEulerAngles = Vector3.zero;
			lineRenderer.useWorldSpace = false;
			lineRenderer.material = new Material (Shader.Find ("Particles/Additive"));
			lineRenderer.SetWidth (0.2f, 0.2f); 
			lineRenderer.SetVertexCount (2);
			lineRenderer.SetPosition (0, Vector3.zero);
			lineRenderer.SetPosition (1, Vector3.zero);
			lineRenderer.SetColors (Color.gray, Color.gray);
		}

		public override void OnUpdate()
		{
			float dt = Time.deltaTime;
			Vessel activeVessel = FlightGlobals.ActiveVessel;
			bool keyDown = Input.GetKeyDown (grappleKey);

			// check for illegal states (eg. when a kerbal dies)
			if (
				(lineState == LineState.GRAPPLED && grappledKerbal == null) ||
				(lineState == LineState.CONNECTED && (connectedShip == null || connectedPart == null))
			   ) {
				lineState = LineState.IDLE;
				grappledKerbal = null;
				connectedShip = null;
				connectedPart = null;
			}

			// cut fuel line if too long
			if (lineState == LineState.GRAPPLED) {
				if ((this.transform.position - grappledKerbal.transform.position).magnitude > Mathf.Abs (maxLineLength)) {
					lineState = LineState.IDLE;
					grappledKerbal = null;
					grappledKerbalsList.Remove (grappledKerbal);
					print ("Snap!");
				}
			} else if (lineState == LineState.CONNECTED) {
				if ((this.transform.position - connectedPart.transform.position).magnitude > Mathf.Abs (maxLineLength)) {
					lineState = LineState.IDLE;
					connectedShip = null;
					connectedPart = null;
					print ("Snap!");
				}
			}

			// state logic
			if (lineState == LineState.IDLE) {

				// key pressed?
				if (keyDown) {

					// player controlling a kerbal?
					if (activeVessel.isEVA) {

						// in range?
						float distance = (this.transform.position - activeVessel.transform.position).magnitude;
						if (distance < Mathf.Abs (maxGrappleDistance)) {

							// kerbal not yet grappled?
							if (grappledKerbalsList.Contains (activeVessel) == false) {

								// grapple fuel line to kerbal
								lineState = LineState.GRAPPLED;
								grappledKerbal = activeVessel;
								connectedShip = null;
								connectedPart = null;
								print ("GRAPPLED at " + distance.ToString () + "m");

								// add to list of grappled kerbals
								grappledKerbalsList.Add (grappledKerbal);
							} else {
								print ("Kerbal already grappled");
							}
						}
					}
				}

			} else if (lineState == LineState.GRAPPLED) {

				// player controlling our kerbal?
				if (activeVessel.isEVA && activeVessel == grappledKerbal) {

					// key pressed?
					if (keyDown) {

						// find closest ship (no kerbals)
						Vessel closestVessel = null;
						float closestVesselDistance = 0;
						foreach (Vessel v in FlightGlobals.Vessels) {
							if (v.isEVA)
								continue;

							Vector3 relPos = v.orbit.pos - activeVessel.orbit.pos;
							float distance = relPos.magnitude;

							if (closestVessel == null || distance < closestVesselDistance) {
								closestVessel = v;
								closestVesselDistance = distance;
							}
						}

						if (closestVessel) {

							// at own vessel?
							if (closestVessel == this.vessel) {

								Vector3 relPos = activeVessel.transform.position - this.transform.position;
								float distance = relPos.magnitude;

								// in range?
								if (distance < Mathf.Abs (maxGrappleDistance)) {
									// remove kerbal from list
									grappledKerbalsList.Remove (grappledKerbal);

									// return the fuel line
									lineState = LineState.IDLE;
									grappledKerbal = null;
									connectedShip = null;
									connectedPart = null;
									print ("IDLE at " + distance.ToString () + "m");
								}
							} else {
								// find closest fuel or rcs tank
								Part closestTank = null;
								float closestTankDistance = 0;

								foreach (Part p in closestVessel.parts) {
									if(p.Resources.Count > 0) {
										Vector3 relPos = activeVessel.transform.position - p.transform.position;
										float distance = relPos.magnitude;

										if (closestTank == null || distance < closestTankDistance) {
											closestTank = p;
											closestTankDistance = distance;
										}
									}
								}

								// in range?
								if (closestTankDistance < Mathf.Abs (maxGrappleDistance) && closestTank != null) {

									// remove kerbal from list
									grappledKerbalsList.Remove (grappledKerbal);

									// connect fuel line to ship
									lineState = LineState.CONNECTED;
									grappledKerbal = null;
									connectedShip = closestVessel;
									connectedPart = closestTank;

									print ("CONNECTED at " + closestTankDistance.ToString ());
								}
							}
						}
					}
				}

			} else if (lineState == LineState.CONNECTED) {

				// key pressed?
				if (keyDown) {

					// player controlling a kerbal?
					if (activeVessel.isEVA) {

						// in range?
						float distance = (connectedPart.transform.position - activeVessel.transform.position).magnitude;
						if (distance < Mathf.Abs (maxGrappleDistance)) {

							// kerbal not yet grappled?
							if (grappledKerbalsList.Contains (activeVessel) == false) {

								// grapple fuel line to kerbal
								lineState = LineState.GRAPPLED;
								grappledKerbal = activeVessel;
								connectedShip = null;
								connectedPart = null;
								print ("GRAPPLED at " + distance.ToString () + "m");

								// add to list of grappled kerbals
								grappledKerbalsList.Add (grappledKerbal);
							} else {
								print ("Kerbal already grappled");
							}
						}
					}
				}
			}

			// render logic
			if (lineState == LineState.GRAPPLED) {
				// render fuel line
				Vector3 relPos = grappledKerbal.transform.position - this.transform.position;
				Vector3 ps = transform.InverseTransformDirection (relPos);
				lineRenderer.SetPosition (1, ps);

			} else if (lineState == LineState.CONNECTED) {
				// render fuel line
				Vector3 relPos = connectedPart.transform.position - this.transform.position;
				Vector3 ps = transform.InverseTransformDirection (relPos);
				lineRenderer.SetPosition (1, ps);

			} else {
				// nothing
				lineRenderer.SetPosition (1, Vector3.zero);
			}
		}

		public override void OnFixedUpdate ()
		{
			float dt = TimeWarp.fixedDeltaTime;
			string resourceName = knownResources[selectedResource];

			// snip
			currentFlow = 0;
			currentAmount = 0;
			currentMaxAmount = 0;

			// transfer logic
			if (lineState == LineState.CONNECTED && connectedPart != null) {

				// get rate
				float rate = 1;
				switch(selectedRate) {
					case 0: rate = 1; break;
					case 1: rate = 10; break;
					default: rate = 100; break;
				}

				// consume / produce
				Util.getVesselResource (connectedPart.vessel, resourceName, out currentAmount, out currentMaxAmount);
				float amount = currentAmount;
				float maxAmount = currentMaxAmount;
				if(transferFromFlag) {
					Util.getVesselResource (vessel, resourceName, out amount, out maxAmount);
				}
				float free = Mathf.Max(maxAmount - amount, 0);
				float amountReq = Mathf.Min(rate * dt, free);
				float amountTaken = 0;
				if(transferToFlag) {
					amountTaken = part.RequestResource(resourceName, amountReq);
					connectedPart.RequestResource(resourceName, -amountTaken);
				}
				if(transferFromFlag) {
					amountTaken = connectedPart.RequestResource(resourceName, amountReq);
					part.RequestResource(resourceName, -amountTaken);
				}
				currentFlow = amountTaken / dt;
			}
		}

		public override void OnSave(ConfigNode Node)
		{
		}

		public override void OnLoad(ConfigNode Node)
		{
		}
	}
}
