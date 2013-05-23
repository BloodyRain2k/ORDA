using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class ORDA_Tutorial2 : TutorialScenario
	{
		List<CharacterAnimationState> happyEmotes = new List<CharacterAnimationState>();
		int happyEmoteIndex = 0;
		int totalPages = 12;
		int pageNum = 0;

		TutorialPage welcome;
		TutorialPage dock1, dock2, dock3, dock4, dock5, dock6, dock7, dock8, dock9, dock10;
		TutorialPage complete, error;
		KFSMEvent onError;

		Vessel thisVessel = null;
		Vessel otherVessel = null;
		Part thisDockingPort = null;
		Part otherDockingPort = null;
		ORDA_computer thisComputer = null;
		ORDA_computer otherComputer = null;

		CharacterAnimationState getHappyEmote ()
		{
			CharacterAnimationState s = happyEmotes [happyEmoteIndex];
			happyEmoteIndex++;
			if (happyEmoteIndex >= happyEmotes.Count) {
				happyEmoteIndex = 0;
			}
			return s;
		}

		string formatWindowTitle(string s)
		{
			pageNum++;
			return "[" + pageNum + "/" + totalPages + "] " + s;
		}

		protected override void OnAssetSetup()
		{
			instructorPrefabName = "Instructor_Gene";
		}

		protected override void OnTutorialSetup ()
		{
			try {

				happyEmotes.Add (instructor.anim_true_thumbsUp);
				happyEmotes.Add (instructor.anim_true_smileA);
				happyEmotes.Add (instructor.anim_true_nodA);
				happyEmotes.Add (instructor.anim_true_thumbUp);
				happyEmotes.Add (instructor.anim_true_smileB);
				happyEmotes.Add (instructor.anim_true_nodB);

				#region welcome
				welcome = new TutorialPage ("welcome");
				welcome.windowTitle = formatWindowTitle ("Intro");
				welcome.OnEnter = (KFSMState st) =>
				{
					instructor.StopRepeatingEmote ();

					// disable some stuff
					InputLockManager.SetControlLock (
						ControlTypes.STAGING | 
						ControlTypes.THROTTLE | 
						ControlTypes.LINEAR |
						ControlTypes.VESSEL_SWITCHING,
						"ORDA_Tutorial2_Lock");
				};
				welcome.OnDrawContent = () =>
				{
					GUILayout.Label (
						"Welcome back!\n\n" +
						"In this tutorial we will look at how the ORDA autopilot can help you dock to another spacecraft.\n\n" +
						"It can either perform a autonomous docking or just maintain the correct orientation of your spacecraft while you control the relative position and velocity using the translational RCS controls.");
					if (GUILayout.Button ("Next")) {
						Tutorial.GoToNextPage ();
					}
				};
				Tutorial.AddPage (welcome);
				#endregion
				#region dock1
				dock1 = new TutorialPage ("dock1");
				dock1.windowTitle = formatWindowTitle ("Docking");
				dock1.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmoteRepeating (instructor.anim_idle_lookAround, 5f);

					// find vessels, ports and computers
					if(!findVesselsPartsAndComputers(true)) {
						handleError();
					}
				};
				dock1.OnDrawContent = () =>
				{
					GUILayout.Label (
						"Let's try the autonomous docking first.\n\n" +
						"This vessel will serve as your docking target. When using the autonomous docking mode it is absolutely mandatory to use some kind of Attitude-Hold on the target vessel. You can either achieve this by using an Advanced Stability Augmentation System (ASAS) or by using the Att->Hold mode of your ORDA autopilot.\n\n" +
						"Use the rotational controls to point your nose in the approximate direction of the other spacecraft (optional) and then activate the Att -> Hold mode.");
				};
				dock1.SetAdvanceCondition ((KFSMState st) => {
					ORDA.GNCconfig gncConfig = otherComputer.getGNCConfig();
					return (gncConfig.command == GNC.Command.ATT && gncConfig.attMode == GNC.AttMode.HOLD);
				});
				Tutorial.AddPage (dock1);
				#endregion
				#region dock2
				dock2 = new TutorialPage ("dock2");
				dock2.windowTitle = formatWindowTitle ("Docking");
				dock2.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());

					// allow vessel switching
					InputLockManager.SetControlLock (
						ControlTypes.STAGING | 
						ControlTypes.THROTTLE | 
						ControlTypes.LINEAR,
						"ORDA_Tutorial2_Lock");
				};
				dock2.OnDrawContent = () =>
				{
					GUILayout.Label ("Good job! Now lets switch over to the other vessel using the [" + GameSettings.FOCUS_NEXT_VESSEL.primary.ToString() + "] key.");
				};
				dock2.SetAdvanceCondition ((KFSMState st) => {
					return (FlightGlobals.ActiveVessel == thisVessel);
				});
				Tutorial.AddPage (dock2);
				#endregion
				#region dock3
				dock3 = new TutorialPage ("dock3");
				dock3.windowTitle = formatWindowTitle ("Docking");
				dock3.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());

					// forbid vessel switching
					InputLockManager.SetControlLock (
						ControlTypes.STAGING | 
						ControlTypes.THROTTLE | 
						ControlTypes.LINEAR |
						ControlTypes.VESSEL_SWITCHING,
						"ORDA_Tutorial2_Lock");
				};
				dock3.OnDrawContent = () =>
				{
					GUILayout.Label (
						"Before you can activate the docking mode you need to select the target vessel, the target docking port and your local docking port.\n\n" +
						"Let's select the local docking port first. Open the target page and select 'local port 1'. Note the matching docking port on your vessel being highlighted in green.");
				};
				dock3.SetAdvanceCondition ((KFSMState st) => {
					return (thisComputer.getVesselDockingPort() == thisDockingPort);
				});
				Tutorial.AddPage (dock3);
				#endregion
				#region dock4
				dock4 = new TutorialPage ("dock4");
				dock4.windowTitle = formatWindowTitle ("Docking");
				dock4.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				dock4.OnDrawContent = () =>
				{
					GUILayout.Label (
						"Nicely done! There are two ways to select the target vessel and it's docking port. You can either select them in the target page like you did with your local docking port or you can select them in-game by right-clicking the docking port on the other vessel and selecting 'Set as target'.\n\n" +
						"Go ahead and try both methods. Note that the target page will only show free docking ports. Lateral ports will only show up if they are open.");

					if(thisComputer.getTargetVessel() == otherVessel &&
					   thisComputer.getTargetDockingPort() == otherDockingPort &&
					   thisComputer.getVesselDockingPort() == thisDockingPort) {
						if(GUILayout.Button("Next")) {
							Tutorial.GoToNextPage();
						}
					}
				};
				Tutorial.AddPage (dock4);
				#endregion
				#region dock5
				dock5 = new TutorialPage ("dock5");
				dock5.windowTitle = formatWindowTitle ("Docking");
				dock5.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				dock5.OnDrawContent = () =>
				{
					GUILayout.Label (
						"So far so good. Head over to the autopilot page and note the new 'Dock' master mode. It will only show up if you have both docking ports selected in the target page.\n\n" +
						"Activate the 'Dock' master mode.");
				};
				dock5.SetAdvanceCondition ((KFSMState st) => {
					GNCconfig gncConfig = thisComputer.getGNCConfig();
					return (gncConfig.command == GNC.Command.DOCK);
				});
				Tutorial.AddPage (dock5);
				#endregion
				#region dock6
				dock6 = new TutorialPage ("dock6");
				dock6.windowTitle = formatWindowTitle ("Docking");
				dock6.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				dock6.OnDrawContent = () =>
				{
					GUILayout.Label (
						"As i have previously mentioned there are two docking modes:\n\n" +
						"ATTITUDE will only maintain your correct orientation while you are in charge of the translational RCS controls.\n\n" +
						"AUTO will (try to) perform a fully autonomous docking.\n\n" +
						"Select 'AUTO' and click on 'Engage'.");
				};
				dock6.SetAdvanceCondition ((KFSMState st) => {
					GNCconfig gncConfig = thisComputer.getGNCConfig();
					return (gncConfig.command == GNC.Command.DOCK &&
					        gncConfig.dockMode == GNC.DockMode.AUTO &&
					        gncConfig.dockState == GNC.DockState.ORIENT);
				});
				Tutorial.AddPage (dock6);
				#endregion
				#region dock7
				dock7 = new TutorialPage ("dock7");
				dock7.windowTitle = formatWindowTitle ("Docking");
				dock7.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				dock7.OnDrawContent = () =>
				{
					GUILayout.Label (
						"Good job! The autopilot will now run through a sequence of states that might lead to a successful capture.\n\n" +
						"If you want to abort the autonomous docking at any point you can click 'AUTO' again and take over manual control.\n\n" +
						"Make sure your RCS is activated, lean back and enjoy the ride.");
				};
				dock7.SetAdvanceCondition ((KFSMState st) => {
					return (thisVessel == null || otherVessel == null);
				});
				Tutorial.AddPage (dock7);
				#endregion
				#region dock8
				dock8 = new TutorialPage ("dock8");
				dock8.windowTitle = formatWindowTitle ("Docking");
				dock8.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());

					// allow linear thrusters
					InputLockManager.SetControlLock (
						ControlTypes.STAGING | 
						ControlTypes.THROTTLE |
						ControlTypes.VESSEL_SWITCHING,
						"ORDA_Tutorial2_Lock");
				};
				dock8.OnDrawContent = () =>
				{
					string s =
						"Looks like a solid connection!\n\n" +
						"The autopilot will automatically deactivate itself if you dock or undock. You can now use Rate->Zero or Att->Hold or ASAS to kill any residual spin.\n\n" +
						"Now undock the two spacecraft and get them seperated by at least 25m using the translational RCS controls.";

					// find vessels, ports and computers again, will succeed after undocking
					if(findVesselsPartsAndComputers(false)) {
						float dist = (thisVessel.transform.position - otherVessel.transform.position).magnitude;
						s += "\n\nCurrent Distance: " + dist.ToString("F1") + "m";
					}

					GUILayout.Label(s);
				};
				dock8.SetAdvanceCondition ((KFSMState st) => {
					if(thisVessel == null || otherVessel == null) {
						return false;
					}
					float dist = (thisVessel.transform.position - otherVessel.transform.position).magnitude;
					return (dist > 25);
				});
				Tutorial.AddPage (dock8);
				#endregion
				#region dock9
				dock9 = new TutorialPage ("dock9");
				dock9.windowTitle = formatWindowTitle ("Docking");
				dock9.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				dock9.OnDrawContent = () =>
				{
					GUILayout.Label (
						"Alright. Now select your target vessel, target docking port and local docking port as you did before and then activate the Dock->Attitude mode on the Autopilot page.");
				};
				dock9.SetAdvanceCondition ((KFSMState st) => {
					GNCconfig gncConfig = thisComputer.getGNCConfig();

					return (thisComputer.getTargetVessel() == otherVessel &&
					        thisComputer.getTargetDockingPort() == otherDockingPort &&
					        thisComputer.getVesselDockingPort() == thisDockingPort &&
					        gncConfig.command == GNC.Command.DOCK &&
					        gncConfig.dockMode == GNC.DockMode.ATTITUDE);
				});
				Tutorial.AddPage (dock9);
				#endregion
				#region dock10
				dock10 = new TutorialPage ("dock10");
				dock10.windowTitle = formatWindowTitle ("Docking");
				dock10.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());

					otherComputer.activateAttitudeMode("Hold");
				};
				dock10.OnDrawContent = () =>
				{
					GUILayout.Label (
						"Looks good! I have activated the Attitude-Hold on the other spacecraft for you.\n\n" +
						"Now try to re-dock using the translational RCS controls while the autopilot will keep you oriented properly.\n\n" +
						"Try setting the camera mode to 'Chase' if you get confused.");
				};
				dock10.SetAdvanceCondition ((KFSMState st) => {
					return (thisVessel == null || otherVessel == null);
				});
				Tutorial.AddPage (dock10);
				#endregion
				#region complete
				complete = new TutorialPage ("complete");
				complete.windowTitle = formatWindowTitle ("Complete");
				complete.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				complete.OnDrawContent = () =>
				{
					GUILayout.Label ("Congratulations! You finished this tutorial.\n\n" +
						"You can close the tutorial window now and play around with your ships if you want."
					);
					if (GUILayout.Button ("Close tutorial")) {
						Destroy (this);
					}
				};
				Tutorial.AddPage (complete);
				#endregion
				#region error
				error = new TutorialPage ("special page 1");
				error.OnEnter = (KFSMState lastSt) =>
				{
					error.windowTitle = "Error (from " + lastSt.name + ")";
					instructor.PlayEmote (instructor.anim_false_sadA);
				};
				error.OnDrawContent = () =>
				{
					GUILayout.Label ("Ooops!\n\nSomething went wrong ...", GUILayout.ExpandHeight (true));
				};
				Tutorial.AddState (error);

				onError = new KFSMEvent ("Something Unplanned");
				onError.updateMode = KFSMUpdateMode.MANUAL_TRIGGER;
				onError.GoToStateOnEvent = error;
				Tutorial.AddEventExcluding (onError, error);
				#endregion

				Tutorial.StartTutorial (welcome);

			} catch (Exception e) {
				print("exception in OnTutorialSetup: " + e.ToString());
			}
		}

		void handleError()
		{
			Tutorial.RunEvent(onError);
		}

		void OnDestroy ()
		{
			InputLockManager.RemoveControlLock("ORDA_Tutorial2_Lock");
		}

		bool findVesselsPartsAndComputers (bool other)
		{
			Vessel _thisVessel = FlightGlobals.ActiveVessel;
			Vessel _otherVessel = null;
			foreach (Vessel v in FlightGlobals.Vessels) {
				if (v != _thisVessel) {
					_otherVessel = v;
					break;
				}
			}
			thisVessel = (other)?(_otherVessel):(_thisVessel);
			otherVessel = (other)?(_thisVessel):(_otherVessel);
			if(thisVessel == null || otherVessel == null) {
				return false;
			}
			foreach(Part p in thisVessel.parts) {
				if(p.name == "dockingPort1") {
					thisDockingPort = p;
					break;
				}
			}
			foreach(Part p in otherVessel.parts) {
				if(p.name == "dockingPort1") {
					otherDockingPort = p;
					break;
				}
			}
			if(otherDockingPort == null || thisDockingPort == null) {
				return false;
			}
			thisComputer = Util.getActiveComputerFromVessel(thisVessel);
			otherComputer = Util.getActiveComputerFromVessel(otherVessel);
			if(otherComputer == null || thisComputer == null) {
				return false;
			}
			return true;
		}
	}
}
