using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class ORDA_computer : PartModule
	{
		// cfg settings
		[KSPField]
		public bool teleporterEnabled = false;
		[KSPField]
		public bool consumePower = true;
		[KSPField]
		public float maxPowerConsumption = 4;
		[KSPField]
		public string resourceName = "ElectricCharge";
		[KSPField]
		public float default_Kp_AngVel = 0.25f;
		[KSPField]
		public float default_Kp_AngAcc = 2.0f;
		[KSPField]
		public float default_Kp_Vel = 0.25f;
		[KSPField]
		public float default_Kp_Acc = 2.0f;

		// unit objects
		VisualHelper visualHelper = null;
		FlightData flightData = null;
		GNC gnc = null;
		Bus bus = null;
		Teleporter teleporter = null;

		// for unit activation, in case there is more than one
		Vessel thisVessel = null;
		int thisVesselParts = 0;
		bool activeSystem = false;

		// gui
		public enum PageType { PAGE_TARGET=0, PAGE_ORBIT, PAGE_AUTOPILOT, PAGE_LAND };
		PageType currentPage = PageType.PAGE_TARGET;
		bool windowIsMinimized = true;
		Rect windowPositionAndSize;
		bool windowPositionInvalid = true;
		bool windowSizeInvalid = true;
		const int fullWindowWidth = 350;
		const int smallWindowWidth = 125;

		// gui target page
		Vector2 targetScrollVector = new Vector2();
		Part highlightedVesselDockingPort = null;
		Part highlightedTargetDockingPort = null;
		bool targetShowDebris = false;
		bool targetShowLanded = false;
		bool targetShowDiffBody = false;
		bool targetShowPacked = true;

		// gui autopilot page
		bool settingsToggle = false;
		bool relVectorsToggle = false;
		bool statsToggle = false;
		string Kp_AngVel_string = null;
		string Kp_AngAcc_string = null;
		string Kp_Vel_string = null;
		string Kp_Acc_string = null;
		string eacPulseLength_string = null;
		string eacPulseLevel_string = null;
		string eacRate_string = null;
		bool showRCSWarning = false;

		// gui landing page
		bool landingStatsToggle = false;
		bool landingImpactToggle = false;

		// target
		bool targetChanged = false;
		Vessel targetVessel = null;
		Part vesselDockingPort = null;
		Part targetDockingPort = null;

		// ...
		bool outOfPowerFlag = false;

		//
		// fly by wire handler
		//
		private void fly (FlightCtrlState s)
		{
			if (!activeSystem)
				return;

			bus.yawReq = Mathf.Clamp(s.yaw, -1.0f, +1.0f);
			bus.pitchReq = Mathf.Clamp(s.pitch, -1.0f, +1.0f);
			bus.rollReq = Mathf.Clamp(s.roll, -1.0f, +1.0f);

			if(bus.yprDriven) {
				if(bus.yprRelative) {
					s.yaw = Mathf.Clamp(s.yaw + bus.yaw, -1.0f, +1.0f);
					s.pitch = Mathf.Clamp(s.pitch + bus.pitch, -1.0f, +1.0f);
					s.roll = Mathf.Clamp(s.roll + bus.roll, -1.0f, +1.0f);
				} else {
					s.yaw = Mathf.Clamp(bus.yaw, -1.0f, +1.0f);
					s.pitch = Mathf.Clamp(bus.pitch, -1.0f, +1.0f);
					s.roll = Mathf.Clamp(bus.roll, -1.0f, +1.0f);
				}
			}

			if(bus.xyzDriven) {
				if(bus.xyzRelative) {
					s.X = Mathf.Clamp(s.X + bus.x, -1.0f, +1.0f);
					s.Y = Mathf.Clamp(s.Y + bus.z, -1.0f, +1.0f);
					s.Z = Mathf.Clamp(s.Z + bus.y, -1.0f, +1.0f);
				} else {
					s.X = Mathf.Clamp(bus.x, -1.0f, +1.0f);
					s.Y = Mathf.Clamp(bus.z, -1.0f, +1.0f);
					s.Z = Mathf.Clamp(bus.y, -1.0f, +1.0f);
				}
			}
		}

		//
		// gui
		//
		private void windowGUI (int windowID)
		{
			PageType oldPage = currentPage;

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

			if (outOfPowerFlag) {
				GUILayout.Label("Power failure.");
				if (GUILayout.Button ("Reset", activeStyle)) {
//					windowPositionInvalid = true;
					windowSizeInvalid = true;
					outOfPowerFlag = false;
				}

			} else {
				GUILayout.BeginVertical ();

				// page selector
				GUILayout.BeginHorizontal ();
				if (GUILayout.Button ("Hide", (style))) {
					windowIsMinimized = true;
				}
				if (GUILayout.Button ("Target", (currentPage == PageType.PAGE_TARGET) ? (activeStyle) : (style))) {
					currentPage = PageType.PAGE_TARGET;
				}
				if (GUILayout.Button ("Orbit", (currentPage == PageType.PAGE_ORBIT) ? (activeStyle) : (style))) {
					currentPage = PageType.PAGE_ORBIT;
				}
				if (GUILayout.Button ("Autopilot", (currentPage == PageType.PAGE_AUTOPILOT) ? (activeStyle) : (style))) {
					currentPage = PageType.PAGE_AUTOPILOT;
				}
				if (GUILayout.Button ("Land", (currentPage == PageType.PAGE_LAND) ? (activeStyle) : (style))) {
					currentPage = PageType.PAGE_LAND;
				}
				GUILayout.EndHorizontal ();

				// page content
				switch (currentPage) {
				case PageType.PAGE_TARGET:
					windowTargetGUI (style, activeStyle);
					break;
				case PageType.PAGE_ORBIT:
					windowOrbitGUI (style, activeStyle);
					break;
				case PageType.PAGE_AUTOPILOT:
					windowAutopilotGUI (style, activeStyle);
					break;
				case PageType.PAGE_LAND:
					windowLandingGUI (style, activeStyle);
					break;
				default:
					break;
				}

				GUILayout.EndVertical ();
			}

			// dragable window
			GUI.DragWindow ();

			// resize window if page changed
			if (oldPage != currentPage) {
				windowSizeInvalid = true;
			}

			// highlight docking ports
			if (currentPage == PageType.PAGE_TARGET && !windowIsMinimized) {
				// remove old highlights
				if(highlightedVesselDockingPort != vesselDockingPort && highlightedVesselDockingPort != null) {
					highlightedVesselDockingPort.SetHighlightDefault();
					highlightedVesselDockingPort.SetHighlight(false);
				}
				if(highlightedTargetDockingPort != targetDockingPort && highlightedTargetDockingPort != null) {
					highlightedTargetDockingPort.SetHighlightDefault();
					highlightedTargetDockingPort.SetHighlight(false);
				}

				// highlight selected ports
				if(vesselDockingPort != null) {
					vesselDockingPort.SetHighlightColor(Color.green);
					vesselDockingPort.SetHighlight(true);
				}
				if(targetDockingPort != null) {
					targetDockingPort.SetHighlightColor(Color.green);
					targetDockingPort.SetHighlight(true);
				}
				highlightedVesselDockingPort = vesselDockingPort;
				highlightedTargetDockingPort = targetDockingPort;

			} else {
				// remove highlights
				if(highlightedVesselDockingPort != null) {
					highlightedVesselDockingPort.SetHighlightDefault();
					highlightedVesselDockingPort.SetHighlight(false);
					highlightedVesselDockingPort = null;
				}
				if(highlightedTargetDockingPort != null) {
					highlightedTargetDockingPort.SetHighlightDefault();
					highlightedTargetDockingPort.SetHighlight(false);
					highlightedTargetDockingPort = null;
				}
			}
		}

		private void windowTargetGUI (GUIStyle style, GUIStyle activeStyle)
		{
			int num;

			// select own docking port
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("Local port: ");
			if (GUILayout.Button ("-", (vesselDockingPort == null) ? (activeStyle) : (style))) {
				vesselDockingPort = null;
			}
			num = 1;
			foreach (Part p in vessel.parts) {
				if(Util.isFreeDockingPort(p)) {
					if (GUILayout.Button (num.ToString (), (vesselDockingPort == p) ? (activeStyle) : (style))) {
						vesselDockingPort = p;
					}
					num++;
				}
			}
			GUILayout.EndHorizontal ();

			// clear vessel selection?
			GUILayout.Label ("Available targets:");
			targetScrollVector = GUILayout.BeginScrollView (targetScrollVector, GUILayout.Height (250));
			if (GUILayout.Button ("-", (targetVessel == null) ? (activeStyle) : (style))) {
				targetVessel = null;
				targetDockingPort = null;
				targetChanged = true;
				Util.unsetVesselTarget();
			}

			List<Vessel> vesselList = Util.getSortedVesselList (this.vessel);
			foreach (Vessel v in vesselList) {
				// any filter applying?
				if (v == this.vessel)
					continue;
				if (v.vesselType == VesselType.Debris && !targetShowDebris)
					continue;
				if (v.Landed && !targetShowLanded)
					continue;
				if (v.packed && !targetShowPacked)
					continue;
				if (v.mainBody != this.vessel.mainBody && !targetShowDiffBody)
					continue;

				// get distance
				double distance = (this.vessel.orbit.pos - v.orbit.pos).magnitude;
				if(v.mainBody != this.vessel.mainBody) {
					distance = ((this.vessel.orbit.pos + this.vessel.mainBody.position) -
					            (v.orbit.pos + v.mainBody.position)).magnitude;
				}

				// select this vessel?
				GUILayout.BeginHorizontal ();
				if (GUILayout.Button ("[" + v.vesselName + "] (" + Util.formatValue (distance, "m", "F3") + ")", (targetVessel == v) ? (activeStyle) : (style))) {
					targetVessel = v;
					targetDockingPort = null;
					targetChanged = true;
//					Util.unsetVesselTarget();
					Util.setVesselTarget(v);
				}
				GUILayout.EndHorizontal ();

				// this one currently selected?
				if (v == targetVessel) {
					if(v.parts == null || v.parts.Count == 0) {
						GUILayout.Label("Must be closer than 2.5km to select docking ports.");
					} else {
						// clear docking port selection?
						GUILayout.BeginHorizontal ();
						GUILayout.Label ("Target port: ");
						if (GUILayout.Button ("-", (targetDockingPort == null) ? (activeStyle) : (style))) {
							targetDockingPort = null;
							targetChanged = true;
//							Util.unsetVesselTarget();
							Util.setVesselTarget(v);
						}
						// select docking ports?
						num = 1;
						foreach (Part p in v.parts) {
							if(Util.isFreeDockingPort(p)) {
								if (GUILayout.Button (num.ToString (), (targetDockingPort == p) ? (activeStyle) : (style))) {
									targetDockingPort = p;
									Util.setVesselTargetByPart(p);
								}
								num++;
							}
						}
						GUILayout.EndHorizontal ();
					}
				}
			}
			GUILayout.EndScrollView ();

			// target selection
			GUILayout.BeginHorizontal ();
			targetShowDebris = GUILayout.Toggle (targetShowDebris, "Debris", GUILayout.ExpandWidth (true));
			targetShowLanded = GUILayout.Toggle (targetShowLanded, "Landed", GUILayout.ExpandWidth (true));
			targetShowPacked = GUILayout.Toggle (targetShowPacked, "Packed", GUILayout.ExpandWidth (true));
			targetShowDiffBody = GUILayout.Toggle (targetShowDiffBody, "d.Body", GUILayout.ExpandWidth(true));
			GUILayout.EndHorizontal ();
			if (targetVessel != null) {
				if ((targetVessel.vesselType == VesselType.Debris && !targetShowDebris) ||
					(targetVessel.Landed && !targetShowLanded) ||
					(targetVessel.packed && !targetShowPacked) ||
				    (targetVessel.mainBody != this.vessel.mainBody && !targetShowDiffBody)) {
					targetVessel = null;
					targetDockingPort = null;
					targetChanged = true;
				}
			}

			// teleporter
			if (teleporterEnabled) {
				GUILayout.BeginHorizontal();
				GUILayout.Label("Teleport to ");
				if (GUILayout.Button ("Orbit") && TimeWarp.CurrentRateIndex == 0) {
					teleporter.teleportToOrbit();
				}
				if (targetVessel != null) {
					if (GUILayout.Button ("Target") && TimeWarp.CurrentRateIndex == 0) {
						teleporter.teleportToTarget();
					}
					if(targetDockingPort != null && vesselDockingPort != null) {
						if(GUILayout.Button ("Dock") && TimeWarp.CurrentRateIndex == 0) {
							TeleporterLite.teleportToDockingPort(vessel, vesselDockingPort, targetDockingPort, 0.5f); //-0.01f);
						}
					}
				}
				GUILayout.EndHorizontal();
			}

			// resize window?
			if (targetChanged) {
				windowSizeInvalid = true;
			}
		}

		private void windowOrbitGUI (GUIStyle style, GUIStyle activeStyle)
		{
			GUILayout.BeginHorizontal ();

			GUILayout.BeginVertical ();
			GUILayout.Label ("");
			GUILayout.Label ("Body");
			GUILayout.Label ("ApA");
			GUILayout.Label ("PeA");
			GUILayout.Label ("Altitude");
			GUILayout.Label ("Time to ApA");
			GUILayout.Label ("Time to PeA");
			GUILayout.Label ("Inclination");
			GUILayout.Label ("LAN");
			GUILayout.Label ("AoP");
			GUILayout.EndVertical ();

			GUILayout.BeginVertical ();
			GUILayout.Label ("Vessel");
			GUILayout.Label (vessel.mainBody.name);
			GUILayout.Label (Util.formatValue (vessel.orbit.ApA, "m", "F3"));
			GUILayout.Label (Util.formatValue (vessel.orbit.PeA, "m", "F3"));
			GUILayout.Label (Util.formatValue (vessel.orbit.altitude, "m", "F3"));
			GUILayout.Label (Util.formatValue (vessel.orbit.timeToAp, "s"));
			GUILayout.Label (Util.formatValue (vessel.orbit.timeToPe, "s"));
			GUILayout.Label (Util.formatValue (vessel.orbit.inclination, "°", "F3"));
			GUILayout.Label (Util.formatValue (vessel.orbit.LAN, "°", "F3"));
			GUILayout.Label (Util.formatValue (vessel.orbit.argumentOfPeriapsis, "°", "F3"));
			GUILayout.EndVertical ();

			GUILayout.BeginVertical ();
			GUILayout.Label ("Target");
			if (targetVessel != null) {
				GUILayout.Label (targetVessel.mainBody.name);
				GUILayout.Label (Util.formatValue (targetVessel.orbit.ApA, "m", "F3"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.PeA, "m", "F3"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.altitude, "m", "F3"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.timeToAp, "s"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.timeToPe, "s"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.inclination, "°", "F3"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.LAN, "°", "F3"));
				GUILayout.Label (Util.formatValue (targetVessel.orbit.argumentOfPeriapsis, "°", "F3"));
			} else {
				for(int i=0; i<9; i++) {
					GUILayout.Label("-");
				}
			}
			GUILayout.EndVertical ();

			GUILayout.EndHorizontal ();
		}

		private void windowAutopilotGUI (GUIStyle style, GUIStyle activeStyle)
		{
			// get some styles
			GUIStyle slabel = new GUIStyle(GUI.skin.label);
			GUIStyle snormal = new GUIStyle ();
			GUIStyle sgreen = new GUIStyle ();
			GUIStyle sred = new GUIStyle ();
			sgreen.normal.textColor = Color.green;
			sred.normal.textColor = Color.red;
			slabel.wordWrap = false;

			// save toggle states
			bool oldSettingsToggle = settingsToggle;
			bool oldStatsToggle = statsToggle;

			// get gnc states
			GNC.Command gncCommand;
			GNC.RateMode gncRateMode;
			GNC.AttMode gncAttMode;
			GNC.EACMode gncEacMode;
			GNC.PosMode gncPosMode;
			GNC.DockMode gncDockMode;
			gnc.getStates (out gncCommand, out gncRateMode, out gncAttMode, out gncEacMode, out gncPosMode, out gncDockMode);

			// command
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("Command: ");
			if (GUILayout.Button ("OFF", (gncCommand == GNC.Command.OFF) ? (activeStyle) : (style))) {
				gnc.requestCommand (GNC.Command.OFF);
				windowSizeInvalid = true;
			}
			if (GUILayout.Button ("RATE", (gncCommand == GNC.Command.RATE) ? (activeStyle) : (style))) {
				gnc.requestCommand (GNC.Command.RATE);
				windowSizeInvalid = true;
			}
			if (GUILayout.Button ("ATT", (gncCommand == GNC.Command.ATT) ? (activeStyle) : (style))) {
				gnc.requestCommand (GNC.Command.ATT);
				windowSizeInvalid = true;
			}
			if (GUILayout.Button ("EAC", (gncCommand == GNC.Command.EAC) ? (activeStyle) : (style))) {
				gnc.requestCommand (GNC.Command.EAC);
				windowSizeInvalid = true;
			}
			if (vesselDockingPort != null && targetDockingPort != null) {
				if (GUILayout.Button ("DOCK", (gncCommand == GNC.Command.DOCK) ? (activeStyle) : (style))) {
					gnc.requestCommand (GNC.Command.DOCK);
					windowSizeInvalid = true;
				}
			}
			GUILayout.EndHorizontal ();

			// rate
			if (gncCommand == GNC.Command.RATE) {
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Rate: ");
				if (GUILayout.Button ("ZERO", (gncRateMode == GNC.RateMode.ZERO) ? (activeStyle) : (style))) {
					gnc.requestRateMode (GNC.RateMode.ZERO);
				}
				if (GUILayout.Button ("ROLL", (gncRateMode == GNC.RateMode.ROLL) ? (activeStyle) : (style))) {
					gnc.requestRateMode (GNC.RateMode.ROLL);
				}
				if (GUILayout.Button ("HOLD", (gncRateMode == GNC.RateMode.HOLD) ? (activeStyle) : (style))) {
					gnc.requestRateMode (GNC.RateMode.HOLD);
				}
				GUILayout.EndHorizontal ();
			}
			// att
			else if (gncCommand == GNC.Command.ATT) {
				float attError = Vector3.Angle(new Vector3(0,1,0), gnc.attError);
				if(attError < 0.5f) {
					activeStyle.normal.textColor = activeStyle.focused.textColor = Color.green;
				}

				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Att: ");
				if (GUILayout.Button ("REF", (gncAttMode == GNC.AttMode.REF) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.REF);
				}
				if (GUILayout.Button ("HOLD", (gncAttMode == GNC.AttMode.HOLD) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.HOLD);
				}
				if (GUILayout.Button ("RP+", (gncAttMode == GNC.AttMode.RPP) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.RPP);
				}
				if (GUILayout.Button ("RP-", (gncAttMode == GNC.AttMode.RPN) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.RPN);
				}
				if (GUILayout.Button ("RV+", (gncAttMode == GNC.AttMode.RVP) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.RVP);
				}
				if (GUILayout.Button ("RV-", (gncAttMode == GNC.AttMode.RVN) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.RVN);
				}
				GUILayout.EndHorizontal ();
				GUILayout.BeginHorizontal ();
				if (GUILayout.Button ("V+", (gncAttMode == GNC.AttMode.VP) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.VP);
				}
				if (GUILayout.Button ("V-", (gncAttMode == GNC.AttMode.VN) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.VN);
				}
				if (GUILayout.Button ("R+", (gncAttMode == GNC.AttMode.RP) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.RP);
				}
				if (GUILayout.Button ("R-", (gncAttMode == GNC.AttMode.RN) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.RN);
				}
				if (GUILayout.Button ("N+", (gncAttMode == GNC.AttMode.NP) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.NP);
				}
				if (GUILayout.Button ("N-", (gncAttMode == GNC.AttMode.NN) ? (activeStyle) : (style))) {
					gnc.requestAttMode (GNC.AttMode.NN);
				}
				GUILayout.EndHorizontal ();

				activeStyle.normal.textColor = activeStyle.focused.textColor = Color.red;
			}
			// eac
			else if (gncCommand == GNC.Command.EAC) {
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Mode: ", slabel);
				if (GUILayout.Button ("PULSE", (gncEacMode == GNC.EACMode.PULSE) ? (activeStyle) : (style))) {
					gnc.requestEacMode (GNC.EACMode.PULSE);
				}
				if (GUILayout.Button ("RATE", (gncEacMode == GNC.EACMode.RATE) ? (activeStyle) : (style))) {
					gnc.requestEacMode (GNC.EACMode.RATE);
				}
				if (GUILayout.Button ("RATE+ATT", (gncEacMode == GNC.EACMode.RATE_ATT) ? (activeStyle) : (style))) {
					gnc.requestEacMode (GNC.EACMode.RATE_ATT);
				}
				GUILayout.EndHorizontal ();
			}
			// dock
			else if (gncCommand == GNC.Command.DOCK) {

				GNC.DockState gncDockState;
				GNC.DockAbort gncDockAbort;
				gnc.getDockState (out gncDockState, out gncDockAbort);

				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Mode: ", slabel);
				if (GUILayout.Button ("ATTITUDE", (gncDockMode == GNC.DockMode.ATTITUDE) ? (activeStyle) : (style))) {
					gnc.requestDockMode (GNC.DockMode.ATTITUDE);
					windowSizeInvalid = true;
				}
				if (GUILayout.Button ("AUTO", (gncDockMode == GNC.DockMode.AUTO) ? (activeStyle) : (style))) {
					gnc.requestDockMode (GNC.DockMode.AUTO);
					windowSizeInvalid = true;
				}
				GUILayout.EndHorizontal ();

				if (gncDockMode == GNC.DockMode.AUTO) {
					if (GUILayout.Button ((gncDockState == GNC.DockState.IDLE) ? ("Engage") : ("Reset"), style)) {
						gnc.requestDockEngage ();
					}
					GUILayout.BeginHorizontal ();
					GUILayout.Label ("IDLE", (gncDockState == GNC.DockState.IDLE) ? (sgreen) : (snormal));
					GUILayout.Label ("ORIENT", (gncDockState == GNC.DockState.ORIENT) ? (sgreen) : (snormal));
					GUILayout.Label ("EVADE", (gncDockState == GNC.DockState.EVADE) ? (sgreen) : (snormal));
					GUILayout.Label ("ENTRY", (gncDockState == GNC.DockState.ENTRY) ? (sgreen) : (snormal));
					GUILayout.Label ("APPROACH", (gncDockState == GNC.DockState.APPROACH) ? (sgreen) : (snormal));
					GUILayout.Label ("DOCKED", (gncDockState == GNC.DockState.DOCKED) ? (sgreen) : (snormal));
//					GUILayout.Label ("DEPART", (gncDockState == GNC.DockState.DEPART) ? (sgreen) : (snormal));
					GUILayout.EndHorizontal ();
					if (gncDockState == GNC.DockState.ABORT) {
						string abortReason = "unknown";
						switch (gncDockAbort) {
						case GNC.DockAbort.DEVIATION:
							abortReason = "Approach deviation > " + Util.formatValue (GNC.dockAbortDeviation, "°");
							break;
						case GNC.DockAbort.ATTITUDE:
							abortReason = "Attitude error > " + Util.formatValue (GNC.dockAbortAttitude, "°");
							break;
						case GNC.DockAbort.LATCH:
							abortReason = "No latch indication";
							break;
						}
						GUILayout.Label ("ABORT: " + abortReason, sred);
					}
				}
			}

			// position
			if (gncCommand != GNC.Command.DOCK && targetVessel != null) {
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Position: ");
				if (GUILayout.Button ("ZERO", (gncPosMode == GNC.PosMode.ZERO) ? (activeStyle) : (style))) {
					gnc.requestPosMode (GNC.PosMode.ZERO);
				}
				if (GUILayout.Button ("HOLD", (gncPosMode == GNC.PosMode.HOLD) ? (activeStyle) : (style))) {
					gnc.requestPosMode (GNC.PosMode.HOLD);
				}
				if (GUILayout.Button ("V-", (gncPosMode == GNC.PosMode.VN) ? (activeStyle) : (style))) {
					gnc.requestPosMode (GNC.PosMode.VN);
				}
				if (GUILayout.Button ("R-", (gncPosMode == GNC.PosMode.RN) ? (activeStyle) : (style))) {
					gnc.requestPosMode (GNC.PosMode.RN);
				}
				if (GUILayout.Button ("RETREAT", (gncPosMode == GNC.PosMode.RETREAT) ? (activeStyle) : (style))) {
					gnc.requestPosMode (GNC.PosMode.RETREAT);
				}
				GUILayout.EndHorizontal ();
			}

			// target infos
			if (targetVessel != null) {
				GUILayout.Label ("Target: '" + targetVessel.vesselName + "'", slabel);
				if (vesselDockingPort != null && targetDockingPort != null) {
					Vector3 relPos;
					float distance;
					Vector3 euler;
					Util.getDockRelPosAndAtt(vesselDockingPort, targetDockingPort, out relPos, out distance, out euler);

					Vector3 relPosInertial = vesselDockingPort.transform.TransformDirection (relPos);
					Vector3 relPosShip = vessel.ReferenceTransform.InverseTransformDirection (relPosInertial);

					GUILayout.Label ("Rel. Att. [°]: " + gnc.pyrError.ToString ("F2"));
					GUILayout.Label ("Rel. Pos. [m]: " + relPosShip.ToString ("F2"));
					GUILayout.Label ("Rel. Vel. [m/s]: " + flightData.targetRelVelocityShip.ToString ("F2"));
					GUILayout.Label ("Distance:  " + Util.formatValue (relPos.magnitude, "m", "F2"), slabel);
					GUILayout.Label ("Approach Speed: " + Util.formatValue (flightData.targetRelVelocityShip.magnitude, "m/s"));
					GUILayout.Label ("Approach Deviation [°]: " + gnc.dockDeviationAngle.ToString ("F2"));
					GUILayout.Label ("Radius: " + gnc.radius.ToString("F2") + "m - TargetRadius: " + gnc.targetRadius.ToString("F2") + "m");
				} else {
					GUILayout.Label ("Rel. Pos. [m]: " + flightData.targetRelPositionShip.ToString ("F2"));
					GUILayout.Label ("Rel. Vel. [m/s]: " + flightData.targetRelVelocityShip.ToString ("F2"));
					GUILayout.Label ("Distance:  " + Util.formatValue (flightData.targetRelPosition.magnitude, "m", "F2"), slabel);
				}
			}

			// toggles
			GUILayout.BeginHorizontal ();
			settingsToggle = GUILayout.Toggle (settingsToggle, "Settings", GUILayout.ExpandWidth (true));
			relVectorsToggle = GUILayout.Toggle (relVectorsToggle, "rel. Vectors", GUILayout.ExpandWidth (true));
			statsToggle = GUILayout.Toggle (statsToggle, "Stats", GUILayout.ExpandWidth (true));
			GUILayout.EndHorizontal ();

			// stats
			if (statsToggle) {
				GUILayout.Label ("angularVelocity: " + flightData.angularVelocity.ToString ("F3"));
				GUILayout.Label ("attError: " + gnc.attError.ToString ("F3"));
				GUILayout.Label ("pyrError: " + gnc.pyrError.ToString ("F3"));
				GUILayout.Label ("avelError: " + gnc.avelError.ToString ("F3"));
				GUILayout.Label ("rposError: " + gnc.rposError.ToString ("F3"));
				GUILayout.Label ("rvelError: " + gnc.rvelError.ToString ("F3"));
				if (flightData.targetVessel != null) {
					GUILayout.Label ("relPosShip: " + flightData.targetRelPositionShip.ToString ("F3"));
					GUILayout.Label ("relVelShip: " + flightData.targetRelVelocityShip.ToString ("F3"));
				}
				GUILayout.Label ("mass: " + Util.formatValue(flightData.mass, "t"));
				GUILayout.Label ("MoI: " + flightData.MoI.ToString ("F3"));
				GUILayout.Label ("availableAngAcc: " + flightData.availableAngAcc.ToString ("F3"));
				GUILayout.Label ("availableLinAcc: " + flightData.availableLinAcc.ToString ("F3"));
				if(consumePower) {
					GUILayout.Label ("powerConsumption: " + (gnc.getPowerFactor() * maxPowerConsumption).ToString("F2") + "/" + maxPowerConsumption.ToString("F2"));
				}
			}

			if(settingsToggle) {

				// controller settings
				float Kp_AngVel = 0;
				float Kp_AngAcc = 0;
				float Kp_Vel = 0;
				float Kp_Acc = 0;
				gnc.getControllerSettings (out Kp_AngVel, out Kp_AngAcc, out Kp_Vel, out Kp_Acc);

				if (Kp_AngVel_string == null) {
					Kp_AngVel_string = Kp_AngVel.ToString ("F3");
					Kp_AngAcc_string = Kp_AngAcc.ToString ("F3");
					Kp_Vel_string = Kp_Vel.ToString ("F3");
					Kp_Acc_string = Kp_Acc.ToString ("F3");
				}

				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Kp_AngVel: " + Kp_AngVel.ToString ("F3"), GUILayout.Width (fullWindowWidth / 2));
				Kp_AngVel_string = GUILayout.TextField (Kp_AngVel_string, GUILayout.ExpandWidth (true));
				GUILayout.EndHorizontal ();
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Kp_AngAcc: " + Kp_AngAcc.ToString ("F3"), GUILayout.Width (fullWindowWidth / 2));
				Kp_AngAcc_string = GUILayout.TextField (Kp_AngAcc_string, GUILayout.ExpandWidth (true));
				GUILayout.EndHorizontal ();
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Kp_Vel: " + Kp_Vel.ToString ("F3"), GUILayout.Width (fullWindowWidth / 2));
				Kp_Vel_string = GUILayout.TextField (Kp_Vel_string, GUILayout.ExpandWidth (true));
				GUILayout.EndHorizontal ();
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Kp_Acc: " + Kp_Acc.ToString ("F3"), GUILayout.Width (fullWindowWidth / 2));
				Kp_Acc_string = GUILayout.TextField (Kp_Acc_string, GUILayout.ExpandWidth (true));
				GUILayout.EndHorizontal ();
				
				GUILayout.BeginHorizontal ();
				if (GUILayout.Button ("Update", style)) {
					double d = 0;
					if (Double.TryParse (Kp_AngVel_string, out d))
						Kp_AngVel = (float)d;
					if (Double.TryParse (Kp_AngAcc_string, out d))
						Kp_AngAcc = (float)d;
					if (Double.TryParse (Kp_Vel_string, out d))
						Kp_Vel = (float)d;
					if (Double.TryParse (Kp_Acc_string, out d))
						Kp_Acc = (float)d;
					gnc.setControllerSettings (Kp_AngVel, Kp_AngAcc, Kp_Vel, Kp_Acc);
				}
				if (GUILayout.Button ("Reset", style)) {
					Kp_AngVel = gnc.Default_Kp_AngVel;
					Kp_AngAcc = gnc.Default_Kp_AngAcc;
					Kp_Vel = gnc.Default_Kp_Vel;
					Kp_Acc = gnc.Default_Kp_Acc;
					gnc.setControllerSettings (Kp_AngVel, Kp_AngAcc, Kp_Vel, Kp_Acc);
					Kp_AngVel_string = null;
				}
				GUILayout.EndHorizontal ();

				// eac settings
				if (gncCommand == GNC.Command.EAC) {
					float eacPulseLength = 0;
					float eacPulseLevel = 0;
					float eacRate = 0;
					gnc.getEACSettings (out eacPulseLength, out eacPulseLevel, out eacRate);

					if (eacPulseLength_string == null) {
						eacPulseLength_string = eacPulseLength.ToString ("F3");
						eacPulseLevel_string = eacPulseLevel.ToString ("F3");
						eacRate_string = eacRate.ToString ("F3");
					}

					GUILayout.BeginHorizontal ();
					GUILayout.Label ("eacPulseLength: " + eacPulseLength.ToString ("F3"), GUILayout.Width (fullWindowWidth / 2));
					eacPulseLength_string = GUILayout.TextField (eacPulseLength_string, GUILayout.ExpandWidth (true));
					GUILayout.EndHorizontal ();
					GUILayout.BeginHorizontal ();
					GUILayout.Label ("eacPulseLevel: " + eacPulseLevel.ToString ("F3"), GUILayout.Width (fullWindowWidth / 2));
					eacPulseLevel_string = GUILayout.TextField (eacPulseLevel_string, GUILayout.ExpandWidth (true));
					GUILayout.EndHorizontal ();
					GUILayout.BeginHorizontal ();
					GUILayout.Label ("eacRate: " + eacRate.ToString ("F3"), GUILayout.Width (fullWindowWidth / 2));
					eacRate_string = GUILayout.TextField (eacRate_string, GUILayout.ExpandWidth (true));
					GUILayout.EndHorizontal ();

					GUILayout.BeginHorizontal ();
					if (GUILayout.Button ("Update", style)) {
						double d = 0;
						if (Double.TryParse (eacPulseLength_string, out d))
							eacPulseLength = (float)d;
						if (Double.TryParse (eacPulseLevel_string, out d))
							eacPulseLevel = (float)d;
						if (Double.TryParse (eacRate_string, out d))
							eacRate = (float)d;
						gnc.setEACSettings (eacPulseLength, eacPulseLevel, eacRate);
					}
					if (GUILayout.Button ("Reset", style)) {
						eacPulseLength = GNC.Default_eacPulseLength;
						eacPulseLevel = GNC.Default_eacPulseLevel;
						eacRate = GNC.Default_eacRate;
						gnc.setEACSettings (eacPulseLength, eacPulseLevel, eacRate);
						eacPulseLength_string = null;
					}
					GUILayout.EndHorizontal ();
				}

				// dock settings
				if (gncCommand == GNC.Command.DOCK) {
					float rollAdjust = 0;
					gnc.getDockSettings(out rollAdjust);

					GUILayout.BeginHorizontal();
					GUILayout.Label ("rollAdjust: " + rollAdjust.ToString("F1"), GUILayout.Width (fullWindowWidth / 2));
					if(GUILayout.Button("+", style)) {
						rollAdjust = ((int)rollAdjust + 15);
						if(rollAdjust > 180) rollAdjust = -165;
						gnc.setDockSettings(rollAdjust);
					}
					if(GUILayout.Button("-", style)) {
						rollAdjust = ((int)rollAdjust - 15);
						if(rollAdjust < -180) rollAdjust = 165;
						gnc.setDockSettings(rollAdjust);
					}
					if(GUILayout.Button("0", style)) {
						rollAdjust = 0;
						gnc.setDockSettings(rollAdjust);
					}
					GUILayout.EndHorizontal();
				}
			}

			// resize window?
			if(oldSettingsToggle != settingsToggle || oldStatsToggle != statsToggle) {
				windowSizeInvalid = true;
			}

			// show rcs warning?
			/*if (FlightInputHandler.RCSLock && 
			    ((gncCommand == GNC.Command.DOCK && gncDockMode == GNC.DockMode.AUTO) ||
			     gncPosMode != GNC.PosMode.IDLE)) {
				showRCSWarning = true;
			} else {
				showRCSWarning = false;
			}*/
		}

		private void windowLandingGUI (GUIStyle style, GUIStyle activeStyle)
		{
			bool oldStatToggle = landingStatsToggle;
			bool oldImpactToggle = landingImpactToggle;

			// visualization
			GUILayout.BeginHorizontal ();
			GUILayout.Label ("AGL: " + Util.formatValue (flightData.altitudeAGL, "m"), GUILayout.Width(fullWindowWidth/3));
			GUILayout.Label ("VS: " + Util.formatValue (flightData.verticalSpeed, "m/s"));
			GUILayout.Label ("HS: " + Util.formatValue (flightData.horizontalSpeed, "m/s"));
			GUILayout.EndHorizontal ();
			GUILayout.BeginHorizontal ();
			landingStatsToggle = GUILayout.Toggle (landingStatsToggle, "Show stats", GUILayout.ExpandWidth (true));
			landingImpactToggle = GUILayout.Toggle (landingImpactToggle, "Simulate impact", GUILayout.ExpandWidth (true));
			GUILayout.EndHorizontal ();

			// some stats that might be useful
			if (landingStatsToggle) {
				GUILayout.Label ("Mass: " + Util.formatValue (flightData.mass, "t"));

				if(vessel.mainBody.atmosphere) {
					GUILayout.Label ("Atmosphere: " + Util.formatValue (vessel.mainBody.maxAtmosphereAltitude, "m"));
				} else {
					GUILayout.Label ("No Atmosphere");
				}
			}

			// simulate impact
			if (landingImpactToggle) {
				float simMinAltitude = 0;
				float simTime = 0;
				float simVelocity = 0;
				bool impact = Util.simulateImpact (flightData, out simMinAltitude, out simTime, out simVelocity);

				// show results
				if (impact) {
					GUILayout.Label ("Time to impact: " + Util.formatValue (simTime, "s"));
					GUILayout.Label ("Impact velocity: " + Util.formatValue (simVelocity, "m/s"));
				} else {
					GUILayout.Label ("No solution, min altitude: " + Util.formatValue (simMinAltitude, "m"));
				}
			}

			if (oldStatToggle != landingStatsToggle || oldImpactToggle != landingImpactToggle) {
				windowSizeInvalid = true;
			}
		}

		private void warningGUI (int windowID)
		{
			GUILayout.Label ("don't forget to activate RCS!");
		}

		private void drawGUI ()
		{
			if (!activeSystem)
				return;

			if (vessel == FlightGlobals.ActiveVessel && vessel.IsControllable) {

				// default skin
				GUI.skin = HighLogic.Skin;

				// magic button
				if (windowIsMinimized) {
					if (GUI.Button (new Rect (Screen.width - 50, 0, 50, 25), "ORDA")) {
						windowIsMinimized = false;
					}
				}

				// fix pos / size
				int windowWidth = (outOfPowerFlag)?(smallWindowWidth):(fullWindowWidth);
				if (windowPositionInvalid) {
					windowPositionInvalid = false;
					windowPositionAndSize.x = Screen.width - windowWidth - 10;
					windowPositionAndSize.y = 30;
				}
				if (windowSizeInvalid) {
					windowSizeInvalid = false;
					windowPositionAndSize.width = 10;
					windowPositionAndSize.height = 10;
				}

				// title
				string windowTitle = "ORDA";
				if (!outOfPowerFlag) {
					windowTitle += " [" + Util.truncateString (vessel.vesselName, 32) + "]";
				}

				// main window
				if(!windowIsMinimized) {
					windowPositionAndSize = GUILayout.Window (windowsIDs.computer, windowPositionAndSize, windowGUI, windowTitle, GUILayout.MinWidth (windowWidth));	 
				}

				// warning box
				if (showRCSWarning) {
					Rect boxPositionAndSize = new Rect();
					boxPositionAndSize.width = 320;
					boxPositionAndSize.height = 20;
					boxPositionAndSize.x = Screen.width - 330;
					boxPositionAndSize.y = Screen.height - 140 - boxPositionAndSize.height;
					GUILayout.Window (windowsIDs.computerBox, boxPositionAndSize, warningGUI, "Hey ...");
				}

			} else {
				// remove highlights
				if (highlightedVesselDockingPort != null) {
					highlightedVesselDockingPort.SetHighlightDefault();
					highlightedVesselDockingPort.SetHighlight(false);
					highlightedVesselDockingPort = null;
				}
				if (highlightedTargetDockingPort != null) {
					highlightedTargetDockingPort.SetHighlightDefault();
					highlightedTargetDockingPort.SetHighlight(false);
					highlightedTargetDockingPort = null;
				}
			}
		}

		[Serializable()]
		public class GUIconfig
		{
			public PageType currentPage;
			public bool windowIsMinimized;
		}

		public void getGUIConfiguration (out GUIconfig config)
		{
			config = new GUIconfig();
			config.currentPage = currentPage;
			config.windowIsMinimized = windowIsMinimized;
		}

		private void restoreGUIConfiguration (GUIconfig config)
		{
			currentPage = config.currentPage;
			windowIsMinimized = config.windowIsMinimized;
			//windowPositionInvalid = true;
			windowSizeInvalid = true;
			// save other stuff too?
		}

		public override void OnAwake() // Awake -> Load -> Start
		{
		}

		public override void OnStart (StartState state)
		{
			if (state == StartState.Editor) return;
			part.force_activate ();

			// create objects
			visualHelper = new VisualHelper (this.vessel.transform);
			if (gnc == null) {
				flightData = new FlightData ();
				bus = new Bus ();
				gnc = new GNC (flightData, bus, this);
			}
			teleporter = new Teleporter (flightData);

			// register gui handler
			RenderingManager.AddToPostDrawQueue (0, new Callback (drawGUI));
		}

		public override void OnUpdate()
		{
			float dt = Time.deltaTime;

			// stop if we are not the active unit
			if (!activeSystem)
				return;

			// stock ksp target selection
			if (FlightGlobals.fetch.VesselTarget is ModuleDockingNode) {
				ModuleDockingNode mdn = (ModuleDockingNode)FlightGlobals.fetch.VesselTarget;
				if (mdn.part != targetDockingPort) {
					targetVessel = mdn.vessel;
					targetDockingPort = mdn.part;
					targetChanged = true;
				}
			}

			// show relative velocity and position vectors?
			visualHelper.updateTransform(vessel.transform);
			if (targetVessel == null) {
				visualHelper.hideLines ();
			} else {
				if (relVectorsToggle) {
					visualHelper.showLineInertial (3, flightData.targetRelPosition.normalized * 5);
					visualHelper.showLineInertial (4, -flightData.targetRelVelocity * 10);
				} else {
					visualHelper.hideLine (3);
					visualHelper.hideLine (4);
				}
			}

			// show inertial reference frame?
			if (statsToggle) {
				visualHelper.showLineInertial (0, new Vector3 (10, 0, 0));
				visualHelper.showLineInertial (1, new Vector3 (0, 10, 0));
				visualHelper.showLineInertial (2, new Vector3 (0, 0, 10));
			} else {
				visualHelper.hideLine (0);
				visualHelper.hideLine (1);
				visualHelper.hideLine (2);
			}
		}

		public override void OnFixedUpdate()
		{
			float dt = Time.fixedDeltaTime;
			bool dockEvent = false;

			// first time, vessel changed or lost some parts?
			if (thisVessel == null || thisVessel != vessel || thisVesselParts != vessel.parts.Count) {

				// find uppermost part
				int firstPartInverseStage = 0;
				Part firstPart = null;
				foreach (Part p in vessel.parts) {
					foreach(PartModule pm in p.Modules) {
						if(pm is ORDA_computer) {
							if (firstPart == null || p.inverseStage < firstPartInverseStage) {
								firstPart = p;
								firstPartInverseStage = p.inverseStage;
							}
						}
					}
				}

				// vessel changed? -> re-register fly handler
				if (thisVessel != null && thisVessel != vessel) {
					thisVessel.OnFlyByWire -= new FlightInputCallback (fly);
					vessel.OnFlyByWire += new FlightInputCallback (fly);
				}

				// thats us?
				if (firstPart == part) {
					// not yet active?
					if (activeSystem == false) {
						// go active and register fly handler
						activeSystem = true;
						vessel.OnFlyByWire += new FlightInputCallback (fly);

						print ("ORDA on " + getNameString () + " going active");
					} else {
						print ("ORDA on " + getNameString () + " already active");
					}
				}
				// not the uppermost part
				else {
					// already active?
					if (activeSystem == true) {
						// go inactive and remove fly handler
						activeSystem = false;
						vessel.OnFlyByWire -= new FlightInputCallback (fly);

						print ("ORDA on " + getNameString () + " going inactive");
					} else {
						print ("ORDA on " + getNameString () + " doing nothing");
					}
				}

				// the vessel reference or the number of parts changed -> assume we (un)docked
				// this will also trigger on regular staging - there might be a better way hm :/
				// skip on first call
				if (thisVessel != null) {
					dockEvent = true;
				}

				thisVessel = vessel;
				thisVesselParts = vessel.parts.Count;
			}

			// stop if we are not the active unit
			if (!activeSystem) {
				gnc.requestCommand (GNC.Command.OFF);
				return;
			}
			else {
				if (gnc.getCommand() != GNC.Command.OFF) {
					part.vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
				}
			}

			// activate rate dampening if detected docking/undocking
			if (dockEvent) {
				dockEvent = false;

				// need to find a proper way to detect docking/undocking first
				gnc.requestCommand (GNC.Command.OFF);
				/*gnc.requestCommand (GNC.Command.RATE);
				gnc.requestRateMode (GNC.RateMode.ZERO);
				print ("reset to rate dampening");*/

//				windowSizeInvalid = true;
				targetVessel = null;
				targetDockingPort = null;
				vesselDockingPort = null;
			}

			// consume power
			bool hasPower = true;
			if (consumePower) {
				float powerFactor = gnc.getPowerFactor ();
				float energyRequest = maxPowerConsumption * powerFactor * dt;
				float energyDrawn = part.RequestResource (resourceName, energyRequest);
				if (energyDrawn < energyRequest * 0.9) {
					hasPower = false;
				}
			}

			// turn off everything to annoy the player
			if (!hasPower) {
				gnc.requestCommand(GNC.Command.OFF);
				currentPage = PageType.PAGE_TARGET;
				targetVessel = null;
				targetDockingPort = null;
				vesselDockingPort = null;

				if(FlightGlobals.fetch.VesselTarget != null) {
					Util.unsetVesselTarget();
				}

				windowSizeInvalid = true;
				outOfPowerFlag = true;
			}

			// update flight data
			flightData.vessel = this.vessel;
			flightData.targetVessel = targetVessel;
			flightData.vesselPart = vesselDockingPort;
			flightData.targetPart = targetDockingPort;
			flightData.targetChanged = targetChanged;
			targetChanged = false;
			if (hasPower) {
				flightData.update (dt);
			}

			// guidance navigation and control update
			bus.reset ();
			if (flightData.valid && hasPower) {
				gnc.update (dt);
				bus.clamp ();
			}

			// magic teleporter
			if (teleporterEnabled) {
				teleporter.update (dt);
			}
		}

		public override void OnSave (ConfigNode node)
		{
			if (gnc == null) {
				return;
			}
			string gncConfigString = "";
			string guiConfigString = "";

			// serialize gnc config
			try {
				GNCconfig gncConfig = null;
				gnc.getConfiguration (out gncConfig);
				if (gncConfig != null) {
					gncConfigString = Convert.ToBase64String (KSP.IO.IOUtils.SerializeToBinary (gncConfig)).Replace ("=", "*").Replace ("/", "|");
				}
			} catch (Exception e) {
				print ("ORDA_computer.OnSave: gnc exception: " + e.ToString());
			}

			// serialize gui config
			try {
				GUIconfig guiConfig = null;
				getGUIConfiguration (out guiConfig);
				if (guiConfig != null) {
					guiConfigString = Convert.ToBase64String (KSP.IO.IOUtils.SerializeToBinary (guiConfig)).Replace ("=", "*").Replace ("/", "|");
				}
			} catch (Exception e) {
				print ("ORDA_computer.OnSave: gui exception: " + e.ToString());
			}

			// save
			ConfigNode n = node.AddNode("config");
			if(gncConfigString.Length > 0) {
				n.AddValue("gncConfig", gncConfigString);
			}
			if(guiConfigString.Length > 0) {
				n.AddValue("guiConfig", guiConfigString);
			}

		}

		public override void OnLoad (ConfigNode node)
		{
			// get config node
			ConfigNode n = node.GetNode ("config");
			if (n == null) {
				return;
			}

			// gnc config
			if (n.HasValue ("gncConfig")) {
				try {
					string gncConfigString = n.GetValue ("gncConfig");
					GNCconfig gncConfig = (GNCconfig)KSP.IO.IOUtils.DeserializeFromBinary (Convert.FromBase64String (gncConfigString.Replace ("*", "=").Replace ("|", "/")));
					// OnStart not been called yet?
					if(gnc == null) {
						flightData = new FlightData ();
						bus = new Bus ();
						gnc = new GNC (flightData, bus, this);
					}
					gnc.restoreConfiguration (gncConfig);
				} catch (Exception e) {
					print ("ORDA_computer.OnLoad: gnc exception: " + e.ToString());
				}
			}

			// gui config
			if (n.HasValue ("guiConfig")) {
				try {
					string guiConfigString = n.GetValue ("guiConfig");
					GUIconfig guiConfig = (GUIconfig)KSP.IO.IOUtils.DeserializeFromBinary (Convert.FromBase64String (guiConfigString.Replace ("*", "=").Replace ("|", "/")));
					restoreGUIConfiguration (guiConfig);
				} catch (Exception e) {
					print ("ORDA_computer.OnLoad: gui exception: " + e.ToString());
				}
			}
		}

		//
		// utils
		//
		private string getNameString ()
		{
			return "[" + vessel.vesselName + ";" + vessel.parts.Count + ";" + part.inverseStage + "]";
		}

		//
		// used by tutorial
		//
		public bool isActiveSystem()
		{
			return activeSystem;
		}

		public bool isMinimized()
		{
			return windowIsMinimized;
		}

		public PageType getCurrentPage()
		{
			return currentPage;
		}

		public GNCconfig getGNCConfig()
		{
			GNCconfig config = null;
			gnc.getConfiguration(out config);
			return config;
		}

		public void activateAttitudeMode (string m)
		{
			if (m == "Hold") {
				gnc.requestCommand(GNC.Command.ATT);
				gnc.requestAttMode(GNC.AttMode.HOLD);
			}
			else if (m == "V+") {
				gnc.requestCommand(GNC.Command.ATT);
				gnc.requestAttMode(GNC.AttMode.VP);
			}
		}

		public Vessel getTargetVessel()
		{
			return targetVessel;
		}

		public Part getTargetDockingPort()
		{
			return targetDockingPort;
		}

		public Part getVesselDockingPort ()
		{
			return vesselDockingPort;
		}
	}
}
