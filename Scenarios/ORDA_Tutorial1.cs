using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ORDA
{
	public class ORDA_Tutorial1 : TutorialScenario
	{
		List<CharacterAnimationState> happyEmotes = new List<CharacterAnimationState>();
		int happyEmoteIndex = 0;
		int totalPages = 14;
		int pageNum = 0;

		TutorialPage welcome, intro1, intro2, intro3, rate1, att1, att2, att3, target1, att4, att5, eac1, off;
		TutorialPage complete, error;
		KFSMEvent onError;

		ORDA_computer computer = null;

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
					InputLockManager.SetControlLock ((ControlTypes.STAGING | 
						ControlTypes.THROTTLE | 
						ControlTypes.LINEAR), "ORDA_Tutorial1_Lock");
				};
				welcome.OnDrawContent = () =>
				{
					GUILayout.Label ("Welcome!\n\nIn this tutorial you will familiarize yourself with the Orbital Rendezvous and Docking Assistant (ORDA) - a basic autopilot system that can make your life in space a lot easier.\n\nLet's get started.");
					if (GUILayout.Button ("Next")) {
						Tutorial.GoToNextPage ();
					}
				};
				Tutorial.AddPage (welcome);
				#endregion
				#region intro1
				intro1 = new TutorialPage ("intro1");
				intro1.windowTitle = formatWindowTitle ("Intro");
				intro1.OnEnter = (KFSMState st) =>
				{
					//instructor.PlayEmoteRepeating(instructor.anim_idle_sigh, 5f);
					//instructor.PlayEmoteRepeating(instructor.anim_idle_wonder, 5f);
					instructor.PlayEmoteRepeating (instructor.anim_idle_lookAround, 5f);

					// make ship spin
					flyTime = 1;
					flyYPR = new Vector3 (0, 0, 0.1f);
					FlightGlobals.ActiveVessel.OnFlyByWire += new FlightInputCallback (fly);

					// find active computer
					computer = Util.getActiveComputerFromVessel(FlightGlobals.ActiveVessel);
					if(computer == null) {
						handleError ();
					}
				};
				intro1.OnDrawContent = () =>
				{
					GUILayout.Label ("We are currently orbiting Kerbin at 500km. As you can see there is a second vessel nearby. Both vessels are equipped with the ORDA computer system. Always make sure you install at least one of them on your vessel if you want to use it's functionality.\n\nThe ORDA control panel will always appear in the upper right corner of your screen. This one is currently minimized to declutter your sight. Click on 'ORDA' to restore it.");
				};
				intro1.SetAdvanceCondition ((KFSMState st) => {
					return !computer.isMinimized (); });
				intro1.OnLeave = (KFSMState st) =>
				{
					FlightGlobals.ActiveVessel.OnFlyByWire -= new FlightInputCallback (fly);
				};
				Tutorial.AddPage (intro1);
				#endregion
				#region intro2
				intro2 = new TutorialPage ("intro2");
				intro2.windowTitle = formatWindowTitle ("Intro");
				intro2.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				intro2.OnDrawContent = () =>
				{
					GUILayout.Label ("Good job! Now don't get scared by all the weird buttons... they are on our side!\n\n" +
						"The ORDA user interface consists of several pages. Because our capsules are so small we can only show one page at a time. The first row of buttons lets you choose between pages.\n\n" +
						"Go ahead and select 'Autopilot' so we can stabilize your vehicle."
					);
				};
				intro2.SetAdvanceCondition ((KFSMState st) => {
					return computer.getCurrentPage () == ORDA_computer.PageType.PAGE_AUTOPILOT; });
				Tutorial.AddPage (intro2);
				#endregion
				#region intro3
				intro3 = new TutorialPage ("intro3");
				intro3.windowTitle = formatWindowTitle ("Intro");
				intro3.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				intro3.OnDrawContent = () =>
				{
					GUILayout.Label ("Great! The Autopilot provides several modes of operation:\n\n" +
						"Rate: Maintain angular velocity\n" +
						"Att: Maintain attitude\n" +
						"EAC: Enhanced attitude control\n" +
						"Dock: Assisted / autonomous docking\n\n" +
						"We want to kill your angular velocity so go ahead and click on 'Rate'"
					);
				};
				intro3.SetAdvanceCondition ((KFSMState st) => {
					GNCconfig config = computer.getGNCConfig ();
					return (config.command == GNC.Command.RATE);
				}
				);
				Tutorial.AddPage (intro3);
				#endregion
				#region rate1
				rate1 = new TutorialPage ("rate1");
				rate1.windowTitle = formatWindowTitle ("Rate");
				rate1.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				rate1.OnDrawContent = () =>
				{
					GUILayout.Label ("Now we have to tell the autopilot what angular velocity we want it to maintain.\n\n" +
						"Select 'Zero' to stop the rotation."
					);
				};
				rate1.SetAdvanceCondition ((KFSMState st) => {
					GNCconfig config = computer.getGNCConfig ();

					if (config.command == GNC.Command.RATE &&
						config.rateMode == GNC.RateMode.ZERO &&
						computer.vessel.angularVelocity.magnitude < 0.1f) {
						return true;
					}
					return false;
				}
				);
				Tutorial.AddPage (rate1);
				#endregion
				#region att1
				att1 = new TutorialPage ("att1");
				att1.windowTitle = formatWindowTitle ("Attitude");
				att1.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				att1.OnDrawContent = () =>
				{
					GUILayout.Label ("Great job! Looks like you're getting quite good at pushing buttons.\n\n" +
						"Now that we have stabilized your vessel, we can take a look at the Attitude mode."
					);
				};
				att1.SetAdvanceCondition ((KFSMState st) => {
					GNCconfig config = computer.getGNCConfig ();

					if (config.command == GNC.Command.ATT) {
						return true;
					}
					return false;
				}
				);
				Tutorial.AddPage (att1);
				#endregion
				#region att2
				att2 = new TutorialPage ("att2");
				att2.windowTitle = formatWindowTitle ("Attitude");
				att2.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				att2.OnDrawContent = () =>
				{
					GUILayout.Label ("Now we need to tell the autopilot what attitude we want it to maintain.\n\n" +
						"Go ahead and try out all the different orientations.\n\n" +
						"REF will orient you to the inertial reference frame. " +
						"HOLD will maintain the orientation you are in when you activate it. " +
						"V/N/R will orient you along your orbital velocity/normal/position vectors.\n\n" +
						"To continue select Prograde (V+)"
					);

					GNCconfig config = computer.getGNCConfig ();
					if (config.command == GNC.Command.ATT && config.attMode == GNC.AttMode.VP) {
						if (GUILayout.Button ("Next")) {
							Tutorial.GoToNextPage ();
						}
					}
				};
				Tutorial.AddPage (att2);
				#endregion
				#region att3
				att3 = new TutorialPage ("att3");
				att3.windowTitle = formatWindowTitle ("Attitude");
				att3.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				att3.OnDrawContent = () =>
				{
					GUILayout.Label ("Great! The ship will now point it's nose in the direction of travel. If the engines were working you could now increase the height of your orbit.\n\n" +
						"I'm sure you noticed that the RP (relative Position) and RV (relative Velocity) buttons are not doing anything.\n\n" +
						"We need to select a target first before we can use them.\n\n" +
						"Go to the Target page."
					);
				};
				att3.SetAdvanceCondition ((KFSMState st) => {
					return computer.getCurrentPage () == ORDA_computer.PageType.PAGE_TARGET;
				}
				);
				Tutorial.AddPage (att3);
				#endregion
				#region target1
				target1 = new TutorialPage ("target1");
				target1.windowTitle = formatWindowTitle ("Target");
				target1.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				target1.OnDrawContent = () =>
				{
					string otherVesselName = "???";
					foreach (Vessel v in FlightGlobals.Vessels) {
						if (v != FlightGlobals.ActiveVessel) {
							otherVesselName = v.vesselName;
						}
					}

					GUILayout.Label ("Since our universe currently only contains two Spacecraft this list is pretty clear. The filter on the bottom might help you in the future.\n\n" +
						"Select the other vessel by clicking on '" + otherVesselName + "' and return the the Autopilot page."
					);
				};
				target1.SetAdvanceCondition ((KFSMState st) => {

					if (computer.getCurrentPage () == ORDA_computer.PageType.PAGE_AUTOPILOT &&
						computer.getTargetVessel () != null) {
						return true;
					}
					return false;
				}
				);
				Tutorial.AddPage (target1);
				#endregion
				#region att4
				att4 = new TutorialPage ("att4");
				att4.windowTitle = formatWindowTitle ("Attitude");
				att4.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				att4.OnDrawContent = () =>
				{
					GUILayout.Label ("Now that we have a target selected we can use the autopilot to point your nose towards the other vessel.\n\n" +
						"Go ahead and activate RP+"
					);
				};
				att4.SetAdvanceCondition ((KFSMState st) => {
					GNCconfig config = computer.getGNCConfig ();
					if (config.command == GNC.Command.ATT && config.attMode == GNC.AttMode.RPP) {
						return true;
					}
					return false;
				}
				);
				Tutorial.AddPage (att4);
				#endregion
				#region att5
				att5 = new TutorialPage ("att5");
				att5.windowTitle = formatWindowTitle ("Attitude");
				att5.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				att5.OnDrawContent = () =>
				{
					GUILayout.Label ("Good job! RP/RV orientations are very useful during Rendezvous, don't forget about them.\n\n" +
						"The last thing we will try today is the Enhanced Attitude Control (EAC) mode.\n\n" +
						"Lets activate the EAC Rate mode."
					);
				};
				att5.SetAdvanceCondition ((KFSMState st) => {
					GNCconfig config = computer.getGNCConfig ();
					if (config.command == GNC.Command.EAC && config.eacMode == GNC.EACMode.RATE) {
						return true;
					}
					return false;
				}
				);
				Tutorial.AddPage (att5);
				#endregion
				#region att6
				eac1 = new TutorialPage ("eac1");
				eac1.windowTitle = formatWindowTitle ("Enhanced Attitude Control");
				eac1.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				eac1.OnDrawContent = () =>
				{
					GUILayout.Label ("In this mode the autopilot will maintain constant angular velocites while you make control inputs and stop any rotation otherwise. That angular velocity can be adjusted in the settings.\n\n" +
						"Hold q/w/e/a/s/d and see what happens. Use this mode to orient your spacecraft towards the direction of travel to continue.\n\n" +
						"The Rate+Att mode is very similar, except it will remember and maintain your current orientation when you release all control inputs."
					);

					Vector3 vbar = Util.reorder (FlightGlobals.ActiveVessel.orbit.vel, 132).normalized;
					Vector3 fwd = FlightGlobals.ActiveVessel.ReferenceTransform.TransformDirection (new Vector3 (0, 1, 0));
					float angle = Vector3.Angle (vbar, fwd);
					GUILayout.Label ("Error: " + angle.ToString ("F1") + "°");

					GNCconfig config = computer.getGNCConfig ();
					if (config.command == GNC.Command.EAC && config.eacMode == GNC.EACMode.RATE && angle < 10) {
						if (GUILayout.Button ("Next")) {
							Tutorial.GoToNextPage ();
						}
					}
				};
				Tutorial.AddPage (eac1);
				#endregion
				#region off
				off = new TutorialPage ("off");
				off.windowTitle = formatWindowTitle ("Turn it off");
				off.OnEnter = (KFSMState st) =>
				{
					instructor.PlayEmote (getHappyEmote ());
				};
				off.OnDrawContent = () =>
				{
					GUILayout.Label ("Nice flying!\n\nNow deactivate the autopilot and minimize the ORDA window (Off & Hide).");
				};
				off.SetAdvanceCondition ((KFSMState st) => {
					GNCconfig config = computer.getGNCConfig ();
					if (config.command == GNC.Command.OFF && computer.isMinimized ()) {
						return true;
					}
					return false;
				}
				);
				Tutorial.AddPage (off);
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
						"Keep in mind that the autopilot consumes a lot of power when active and a little amount when in standby. If you run out of power the autopilot will stop working. The maximum power consumption will be " + computer.maxPowerConsumption.ToString("F0") + "/s when you are performing a fully autonomous docking, other modes will require less power. Four RTG's or two medium sized solar panels will deliver enough power for all your autopiloting needs.\n\n" +
						"Next time we will learn how to dock. You can close the tutorial window now and play around with your ship if you want."
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
			InputLockManager.RemoveControlLock("ORDA_Tutorial1_Lock");
		}

		float flyTime = 0;
		Vector3 flyYPR = Vector3.zero;
    	void fly(FlightCtrlState s)
    	{
	        if (Tutorial.TimeAtCurrentState < flyTime)
        	{
				s.yaw = flyYPR.x;
				s.pitch = flyYPR.y;
				s.roll = flyYPR.z;
        	}
    	}
	}
}
