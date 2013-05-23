using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine; 

namespace ORDA
{
	public class ORDA_dump : PartModule
	{
		// cfg settings
		[KSPField]
		public float lfRate;
		[KSPField]
		public float oxRate;
		[KSPField]
		public float mpRate;

		// ...
		static PartModule guiOwner = null;
		Rect windowPositionAndSize = new Rect();
		bool windowPositionInvalid = true;
		bool windowSizeInvalid = true;
		bool windowVisible = false;
		const int windowWidth = 100;

		static Dictionary<Vessel,bool> lfDumpDict = new Dictionary<Vessel,bool>();
		static Dictionary<Vessel,bool> oxDumpDict = new Dictionary<Vessel,bool>();
		static Dictionary<Vessel,bool> mpDumpDict = new Dictionary<Vessel,bool>();

		// events
		[KSPEvent(name = "toggleDumpUI", active = true, guiActive = true, guiName = "Toggle Dump UI", category = "ORDA")]
		public void toggleDumpUI ()
		{
			windowVisible = !windowVisible;

			if (!windowVisible) {
				lfDumpDict[vessel] = false;
				oxDumpDict[vessel] = false;
				mpDumpDict[vessel] = false;
			}
		}

		// gui
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

			GUILayout.BeginHorizontal ();

			bool lfDumpFlag = false;
			bool oxDumpFlag = false;
			bool mpDumpFlag = false;
			if (lfDumpDict.ContainsKey (vessel))
				lfDumpFlag = lfDumpDict [vessel];
			if (oxDumpDict.ContainsKey (vessel))
				oxDumpFlag = oxDumpDict [vessel];
			if (mpDumpDict.ContainsKey (vessel))
				mpDumpFlag = mpDumpDict [vessel];

			if (GUILayout.Button ("LF", (lfDumpFlag) ? (activeStyle) : (style), GUILayout.ExpandWidth (true))) {
				lfDumpFlag = !lfDumpFlag;
				lfDumpDict [vessel] = lfDumpFlag;
			}
			if (GUILayout.Button ("OX", (oxDumpFlag) ? (activeStyle) : (style), GUILayout.ExpandWidth (true))) {
				oxDumpFlag = !oxDumpFlag;
				oxDumpDict [vessel] = oxDumpFlag;
			}
			if (GUILayout.Button ("MP", (mpDumpFlag) ? (activeStyle) : (style), GUILayout.ExpandWidth (true))) {
				mpDumpFlag = !mpDumpFlag;
				mpDumpDict [vessel] = mpDumpFlag;
			}
			if (GUILayout.Button ("X", style, GUILayout.ExpandWidth (true))) {
				toggleDumpUI();
			}
			GUILayout.EndHorizontal ();

			GUILayout.EndVertical();

			GUI.DragWindow();
		}

		private void drawGUI ()
		{
			if ((vessel == FlightGlobals.ActiveVessel) && vessel.IsControllable && windowVisible) {
				if (windowPositionInvalid) {
					windowPositionInvalid = false;
					windowPositionAndSize.x = 140;
					windowPositionAndSize.y = 0;
				}
				if(windowSizeInvalid) {
					windowSizeInvalid = false;
					windowPositionAndSize.width = 10;
					windowPositionAndSize.height = 10;
				}
				GUI.skin = HighLogic.Skin;
				windowPositionAndSize = GUILayout.Window (windowsIDs.dump, windowPositionAndSize, windowGUI, "Fuel Dump");	 
			}
		}

		public override void OnAwake()
		{
		}

		public override void OnStart(StartState state)
		{
			part.force_activate();
		}

		public override void OnUpdate()
		{
			if (FlightGlobals.ActiveVessel == this.vessel) {
				// register gui
				if(guiOwner == null) {
					guiOwner = this;
					RenderingManager.AddToPostDrawQueue(0, new Callback(drawGUI));
				}
			} else {
				// release gui
				if(guiOwner == this) {
					guiOwner = null;
					RenderingManager.RemoveFromPostDrawQueue (0, new Callback (drawGUI));
				}
			}
		}

		public override void OnFixedUpdate ()
		{
			float dt = Time.fixedDeltaTime;

			bool lfDumpFlag = false;
			bool oxDumpFlag = false;
			bool mpDumpFlag = false;

			if (lfDumpDict.ContainsKey (vessel))
				lfDumpFlag = lfDumpDict [vessel];
			if (oxDumpDict.ContainsKey (vessel))
				oxDumpFlag = oxDumpDict [vessel];
			if (mpDumpDict.ContainsKey (vessel))
				mpDumpFlag = mpDumpDict [vessel];

			if (lfDumpFlag) {
				float amount = Mathf.Abs (lfRate) * dt;
				part.RequestResource ("LiquidFuel", amount);
			}
			if (oxDumpFlag) {
				float amount = Mathf.Abs (oxRate) * dt;
				part.RequestResource ("Oxidizer", amount);
			}
			if (mpDumpFlag) {
				float amount = Mathf.Abs (mpRate) * dt;
				part.RequestResource ("MonoPropellant", amount);
			}

			//
			// ElectricCharge
			// LiquidFuel
			// Oxidizer
			// MonoPropellant
			//
		}

		public override void OnSave(ConfigNode Node)
		{
		}

		public override void OnLoad(ConfigNode Node)
		{
		}
	}
}
