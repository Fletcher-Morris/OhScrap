using KSP.Localization;
using KSP.UI.Screens;
using ScrapYard;
using ScrapYard.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OhScrap
{
    //This is a KSPAddon that does everything that PartModules don't need to. Mostly handles the UI
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    internal class EditorAnyWarnings : Utils
    {

    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal class FlightWarnings : Utils
    {

    }

    internal class Utils : MonoBehaviour
    {
        private const string _failureChanceStringFormat = "Failure Chance: {0}, Rolled: {1} Succeeded: {2}";
        private const string _failureEventStringFormat = "Failure Event! Safety Rating: {0}, MET: {1} ";
        private const string _autoLOC_6001674 = "#autoLOC_6001674";
        private const string _autoLOC_6002340 = "#autoLOC_6002340";
        private const string _autoLOC_900685 = "#autoLOC_900685";
        private const string _autoLOC_149410 = "#autoLOC_149410";
        private const string _OHS_01 = "#OHS-01";
        private const string _OHS_02 = "#OHS-02";
        private const string _OHS_03 = "#OHS-03";
        private const string _OHS_04 = "#OHS-04";
        private const string _OHS_05 = "#OHS-05";
        private const string _OHS_06 = "#OHS-06";
        private const string _OHS_08 = "#OHS-08";
        private const string _OHS_09 = "#OHS-09";
        private const string _OHS_10 = "#OHS-10";
        private const string _OHS_11 = "#OHS-11";
        private const string _OHS_12 = "#OHS-12";
        private const string _OHS_13 = "#OHS-13";
        private const string _OHS_14 = "#OHS-14";
        private const string _OHS_15 = "#OHS-15";
        private const string _OHS_16 = "#OHS-16";
        private const string _OHS_17 = "#OHS-17";
        private const string _OHS_18 = "#OHS-18";
        private const string _OHS_19 = "#OHS-19";
        private const string _OHS_20 = "#OHS-20";
        private const string _OHS_21 = "#OHS-21";

        //These hold all "stats" for parts that have already been generated (to stop them getting different results each time)
        public Dictionary<uint, int> generations = new Dictionary<uint, int>();
        public HashSet<uint> testedParts = new HashSet<uint>();
        public int vesselSafetyRating = -1;
        private double nextFailureCheck = 0;
        private Part worstPart;
        public bool display = false;
        private bool dontBother = false;
        public static Utils instance;
        private Rect Window = new Rect(500, 100, 480, 50);
        private ApplicationLauncherButton ToolbarButton;
        private ShipConstruct editorConstruct;
        public bool editorWindow = false;
        public bool flightWindow = true;
        private bool highlightWorstPart = false;
        public System.Random _randomiser = new System.Random();
        public float minimumFailureChance = 0.01f;
        private int timeBetweenChecksPlanes = 10;
        private int timeBetweenChecksRocketsAtmosphere = 10;
        private int timeBetweenChecksRocketsLocalSpace = 1800;
        private int timeBetweenChecksRocketsDeepSpace = 25400;
        public bool ready = false;
        public bool debugMode = false;
        private bool advancedDisplay = false;
        public double timeToOrbit = 300;
        private double chanceOfFailure = 0;
        private string failureMode = "Space/Landed";
        private double displayFailureChance = 0;
        private string sampleTime = "1 year";
        public static bool visibleUI = true;
        private readonly List<BaseFailureModule> _failureModules = new();
        private readonly List<BaseFailureModule> _modules = new();
        private readonly List<BaseFailureModule> _bfmList = new();
        private int _checkFailureInterval = 10;

        private void Awake()
        {
            instance = this;
            ReadDefaultCfg();
        }

        private void ReadDefaultCfg()
        {
            ConfigNode cn = ConfigNode.Load(KSPUtil.ApplicationRootPath + "/GameData/OhScrap/Plugins/PluginData/DefaultSettings.cfg");

            if (cn == null)
            {
                Debug.Log("[OhScrap]: Default Settings file is missing. Using hardcoded defaults");
                ready = true;
                return;
            }

            float.TryParse(cn.GetValue("minimumFailureChance"), out minimumFailureChance);
            Debug.Log("[OhScrap]: minimumFailureChance: " + minimumFailureChance);

            int.TryParse(cn.GetValue("timeBetweenChecksPlanes"), out timeBetweenChecksPlanes);
            Debug.Log("[OhScrap]: timeBetweenChecksPlanes: " + timeBetweenChecksPlanes);

            int.TryParse(cn.GetValue("timeBetweenChecksRocketsAtmosphere"), out timeBetweenChecksRocketsAtmosphere);
            Debug.Log("[OhScrap]: timeBetweenChecksRocketsAtmosphere: " + timeBetweenChecksRocketsAtmosphere);

            int.TryParse(cn.GetValue("timeBetweenChecksRocketsLocalSpace"), out timeBetweenChecksRocketsLocalSpace);
            Debug.Log("[OhScrap]: timeBetweenChecksRocketsLocalSpace: " + timeBetweenChecksRocketsLocalSpace);

            int.TryParse(cn.GetValue("timeBetweenChecksRocketsDeepSpace"), out timeBetweenChecksRocketsDeepSpace);
            Debug.Log("[OhScrap]: timeBetweenChecksRocketsDeepSpace: " + timeBetweenChecksRocketsDeepSpace);

            double.TryParse(cn.GetValue("timeToOrbit"), out timeToOrbit);
            Debug.Log("[OhScrap]: timeToOrbit: " + timeToOrbit);

            bool.TryParse(cn.GetValue("debugMode"), out debugMode);
            Debug.Log("[OhScrap]: debugMode: " + debugMode);

            ready = true;
        }

        private void Start()
        {
            GameEvents.onHideUI.Add(new EventVoid.OnEvent(OnHideUI));
            GameEvents.onShowUI.Add(new EventVoid.OnEvent(OnShowUI));

            GameEvents.onPartDie.Add(OnPartDie);
            GameEvents.onGUIApplicationLauncherReady.Add(GUIReady);
            GameEvents.OnFlightGlobalsReady.Add(OnFlightGlobalsReady);
            GameEvents.onVesselSituationChange.Add(SituationChange);

            //Remembers if the player had the windows opened for closed last time they loaded this scene.
            if (!HighLogic.LoadedSceneIsEditor)
            {
                display = flightWindow;
            }
            else
            {
                display = editorWindow;
            }

            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                InvokeRepeating("CheckForFailures", _checkFailureInterval, _checkFailureInterval);
            }
        }

        private void SituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> data)
        {
            if (data.host != FlightGlobals.ActiveVessel)
            {
                return;
            }

            nextFailureCheck = Planetarium.GetUniversalTime() + _checkFailureInterval;
        }

        private void CheckForFailures()
        {
            if (!FlightGlobals.ready)
            {
                return;
            }

            if (FlightGlobals.ActiveVessel.FindPartModuleImplementing<ModuleUPFMEvents>() != null
                && !FlightGlobals.ActiveVessel.FindPartModuleImplementing<ModuleUPFMEvents>().tested)
            {
                return;
            }

            if (Planetarium.GetUniversalTime() < nextFailureCheck)
            {
                return;
            }

            if (vesselSafetyRating == -1)
            {
                return;
            }

            _failureModules.Clear();
            _failureModules.AddRange(FlightGlobals.ActiveVessel.FindPartModulesImplementing<BaseFailureModule>());

            if (_failureModules.Count == 0)
            {
                return;
            }

            if (!VesselIsLaunched())
            {
                return;
            }

            chanceOfFailure = 0.11 - (vesselSafetyRating * 0.01);

            if (chanceOfFailure < minimumFailureChance)
            {
                chanceOfFailure = minimumFailureChance;
            }

            SetNextCheck(_failureModules);
            double failureRoll = _randomiser.NextDouble();

            if (HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().logging)
            {
                Logger.instance.Log(string.Format(_failureChanceStringFormat, chanceOfFailure, failureRoll, (failureRoll <= chanceOfFailure).ToString()));
            }

            if (failureRoll > chanceOfFailure) return;

            Logger.instance.Log(string.Format(_failureEventStringFormat, vesselSafetyRating, FlightGlobals.ActiveVessel.missionTime));

            BaseFailureModule failedModule = null;
            int counter = _failureModules.Count() - 1;
            _failureModules.Clear();
            _failureModules.AddRange(_failureModules.OrderBy(f => f.chanceOfFailure));

            while (counter >= 0)
            {
                failedModule = _failureModules[counter];
                counter--;

                if (failedModule.hasFailed)
                {
                    continue;
                }

                if (failedModule.isSRB)
                {
                    continue;
                }

                if (failedModule.excluded)
                {
                    continue;
                }

                if (!failedModule.launched)
                {
                    return;
                }

                if (!failedModule.FailureAllowed())
                {
                    continue;
                }

                if (_randomiser.NextDouble() < failedModule.chanceOfFailure)
                {
                    if (failedModule.hasFailed)
                    {
                        continue;
                    }

                    StartFailure(failedModule);
                    Logger.instance.Log("Failing " + failedModule.part.partInfo.title);
                    break;
                }
            }
            if (counter < 0)
            {
                Logger.instance.Log("No parts failed this time. Aborted failure");
            }
        }

        private bool VesselIsLaunched()
        {
            _modules.AddRange(FlightGlobals.ActiveVessel.FindPartModulesImplementing<BaseFailureModule>());

            for (int i = 0; i < _modules.Count; i++)
            {
                if (!_modules[i].launched)
                {
                    return false;
                }
            }

            return true;
        }

        private void SetNextCheck(List<BaseFailureModule> failureModules)
        {
            double chanceOfEvent = 0;
            double chanceOfIndividualFailure = 0;
            double exponent = 0;
            double preparedNumber;
            int moduleCount = 0;

            for (int i = 0; i < failureModules.Count(); i++)
            {
                BaseFailureModule failedModule = failureModules[i];

                if (failedModule.hasFailed)
                {
                    continue;
                }

                if (failedModule.isSRB)
                {
                    continue;
                }

                if (failedModule.excluded)
                {
                    continue;
                }

                if (!failedModule.launched)
                {
                    return;
                }

                if (!failedModule.FailureAllowed())
                {
                    continue;
                }

                moduleCount++;
            }

            preparedNumber = 1 - chanceOfFailure;
            preparedNumber = Math.Pow(preparedNumber, moduleCount);
            chanceOfIndividualFailure = 1 - preparedNumber;

            if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.FLYING && FlightGlobals.ActiveVessel.mainBody == FlightGlobals.GetHomeBody())
            {
                if (FlightGlobals.ActiveVessel.missionTime < timeToOrbit)
                {
                    nextFailureCheck = Planetarium.GetUniversalTime() + timeBetweenChecksRocketsAtmosphere;
                    failureMode = _autoLOC_6001674;
                    sampleTime = (timeToOrbit / 60) + _autoLOC_6002340;
                }
                else
                {
                    nextFailureCheck = Planetarium.GetUniversalTime() + timeBetweenChecksPlanes;
                    failureMode = _autoLOC_900685;
                    sampleTime = _OHS_01;
                }
            }
            else if (VesselIsInLocalSpace())
            {
                nextFailureCheck = Planetarium.GetUniversalTime() + timeBetweenChecksRocketsLocalSpace;
                failureMode = _OHS_06;
                sampleTime = _OHS_02;
            }
            else
            {
                nextFailureCheck = Planetarium.GetUniversalTime() + timeBetweenChecksRocketsDeepSpace;
                failureMode = _OHS_05;
                sampleTime = _OHS_03;
            }

            switch (failureMode)
            {
                //case "Atmosphere":
                case _autoLOC_6001674:
                    exponent = timeToOrbit / timeBetweenChecksRocketsAtmosphere;
                    break;
                //case "Plane":
                case _autoLOC_900685:
                    exponent = 900 / timeBetweenChecksPlanes;
                    break;
                //case "Local Space":
                case _OHS_06:
                    exponent = FlightGlobals.GetHomeBody().solarDayLength * 7 / timeBetweenChecksRocketsLocalSpace;
                    break;
                //case "Deep Space":
                case _OHS_05:
                    exponent = FlightGlobals.GetHomeBody().orbit.period * 3 / timeBetweenChecksRocketsDeepSpace;
                    break;
            }

            preparedNumber = vesselSafetyRating * 0.01;
            preparedNumber = 0.11f - preparedNumber;
            preparedNumber = 1 - preparedNumber;
            preparedNumber = Math.Pow(preparedNumber, exponent);
            chanceOfEvent = 1 - preparedNumber;
            displayFailureChance = Math.Round(chanceOfEvent * chanceOfIndividualFailure * 100, 0);

            if (HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().logging)
            {
                Logger.instance.Log("[OhScrap]: Next Failure Check in " + (nextFailureCheck - Planetarium.GetUniversalTime()));
                Logger.instance.Log("[OhScrap]: Calculated chance of failure in next " + sampleTime + " is " + displayFailureChance + "%");
            }
        }

        private bool VesselIsInLocalSpace()
        {
            CelestialBody cb = FlightGlobals.ActiveVessel.mainBody;
            CelestialBody homeworld = FlightGlobals.GetHomeBody();

            if (cb == homeworld)
            {
                return true;
            }

            List<CelestialBody> children = homeworld.orbitingBodies;
            CelestialBody child;

            for (int i = 0; i < children.Count; i++)
            {
                child = children.ElementAt(i);
                if (child == cb) return true;
            }

            return false;
        }

        private void StartFailure(BaseFailureModule failedModule)
        {
            failedModule.FailPart();
            failedModule.hasFailed = true;
            ModuleUPFMEvents eventModule = failedModule.part.FindModuleImplementing<ModuleUPFMEvents>();
            eventModule.highlight = true;
            eventModule.SetFailedHighlight();
            eventModule.Events["ToggleHighlight"].active = true;
            eventModule.Events["RepairChecks"].active = true;

            eventModule.doNotRecover = true;
            ScreenMessages.PostScreenMessage(failedModule.part.partInfo.title + ": " + failedModule.failureType);
            StringBuilder msg = new StringBuilder();
            msg.AppendLine(failedModule.part.vessel.vesselName);
            msg.AppendLine("");
            msg.AppendLine(failedModule.part.partInfo.title + " " + _OHS_06 + " " + failedModule.failureType);
            msg.AppendLine("");
            MessageSystem.Message m = new MessageSystem.Message(_OHS_09, msg.ToString(), MessageSystemButton.MessageButtonColor.ORANGE, MessageSystemButton.ButtonIcons.ALERT);
            MessageSystem.Instance.AddMessage(m);
            Debug.Log("[OhScrap]: " + failedModule.SYP.ID + " of type " + failedModule.part.partInfo.title + " has suffered a " + failedModule.failureType);
            TimeWarp.SetRate(0, true);
            Logger.instance.Log("Failure Successful");
        }

        private void OnFlightGlobalsReady(bool data)
        {
            vesselSafetyRating = -1;
        }

        //This keeps track of which generation the part is.
        //If its been seen before it will be in the dictionary, so we can just return that (rather than having to guess by builds and times recovered)
        //Otherwise we can assume it's a new part and the "current" build count should be correct.
        public int GetGeneration(uint id, Part part)
        {
            if (generations.TryGetValue(id, out int generation))
            {
                return generation;
            }

            if (HighLogic.LoadedSceneIsEditor)
            {
                generation = ScrapYardWrapper.GetBuildCount(part, ScrapYardWrapper.TrackType.NEW) + 1;
            }
            else
            {
                generation = ScrapYardWrapper.GetBuildCount(part, ScrapYardWrapper.TrackType.NEW);
            }

            generations.Add(id, generation);
            return generation;
        }

        //This is mostly for use in the flight scene, will only run once assuming everything goes ok.
        private void Update()
        {
            int bfmCount = 0;
            vesselSafetyRating = 0;
            double worstPartChance = 0;

            if (!HighLogic.LoadedSceneIsEditor && FlightGlobals.ready)
            {
                _bfmList.Clear();

                for (int i = 0; i < FlightGlobals.ActiveVessel.parts.Count(); i++)
                {
                    Part p = FlightGlobals.ActiveVessel.parts[i];
                    _bfmList.AddRange(p.FindModulesImplementing<BaseFailureModule>());
                }

                for (int i = 0; i < _bfmList.Count(); i++)
                {
                    BaseFailureModule bfm = _bfmList[i];

                    if (bfm == null)
                    {
                        continue;
                    }

                    if (!bfm.ready)
                    {
                        return;
                    }

                    if (bfm.chanceOfFailure > worstPartChance && !bfm.isSRB && !bfm.hasFailed)
                    {
                        worstPart = bfm.part;
                        worstPartChance = bfm.chanceOfFailure;
                    }

                    vesselSafetyRating += bfm.safetyRating;
                    bfmCount++;
                }
            }

            if (HighLogic.LoadedSceneIsEditor)
            {
                if (editorConstruct == null || editorConstruct.parts.Count() == 0) editorConstruct = EditorLogic.fetch.ship;

                _bfmList.Clear();

                for (int i = 0; i < editorConstruct.parts.Count(); i++)
                {
                    Part p = editorConstruct.parts.ElementAt(i);
                    _bfmList.AddRange(p.FindModulesImplementing<BaseFailureModule>());
                }

                for (int i = 0; i < _bfmList.Count(); i++)
                {
                    BaseFailureModule bfm = _bfmList[i];

                    if (bfm == null)
                    {
                        continue;
                    }

                    if (!bfm.ready)
                    {
                        return;
                    }

                    if (bfm.chanceOfFailure > worstPartChance)
                    {
                        worstPart = bfm.part;
                        worstPartChance = bfm.chanceOfFailure;
                    }

                    vesselSafetyRating += bfm.safetyRating;
                    bfmCount++;
                }

                if (bfmCount == 0)
                {
                    editorConstruct = null;
                }

                if (bfmCount == 0)
                {
                    vesselSafetyRating = -1;
                }
                else
                {
                    vesselSafetyRating = vesselSafetyRating / bfmCount;
                }
            }

            if (worstPart != null)
            {
                if (highlightWorstPart && worstPart.highlightType == Part.HighlightType.OnMouseOver)
                {
                    worstPart.SetHighlightColor(Color.yellow);
                    worstPart.SetHighlightType(Part.HighlightType.AlwaysOn);
                    worstPart.SetHighlight(true, false);
                }

                if (!highlightWorstPart && worstPart.highlightType == Part.HighlightType.AlwaysOn
                    && !worstPart.FindModuleImplementing<ModuleUPFMEvents>().highlightOverride)
                {
                    worstPart.SetHighlightType(Part.HighlightType.OnMouseOver);
                    worstPart.SetHighlightColor(Color.green);
                    worstPart.SetHighlight(false, false);
                }
            }
        }

        //Removes the parts from the trackers when they die.
        private void OnPartDie(Part part)
        {
            ModuleSYPartTracker SYP = part.FindModuleImplementing<ModuleSYPartTracker>();

            if (SYP == null)
            {
                return;
            }

            generations.Remove(SYP.ID);
#if DEBUG
            Debug.Log("[UPFM]: Stopped Tracking " + SYP.ID);
#endif
        }

        //Add the toolbar button to the GUI
        public void GUIReady()
        {
            ToolbarButton = ApplicationLauncher.Instance.AddModApplication(GUISwitch, GUISwitch, null, null, null, null, ApplicationLauncher.AppScenes.ALWAYS, GameDatabase.Instance.GetTexture("OhScrap/Plugins/Icon", false));
        }

        //switch the UI on/off
        public void GUISwitch()
        {
            display = !display;
            ToggleWindow();
        }

        //shouldn't really be using OnGUI but I'm too lazy to learn PopUpDialog
        private void OnGUI()
        {
            if (!visibleUI) return;
            if (!HighLogic.CurrentGame.Parameters.CustomParams<OhScrapSettings>().safetyWarning) return;
            if (HighLogic.CurrentGame.Mode == Game.Modes.MISSION) return;
            if (dontBother) return;
            if (!display) return;

            //Display goes away if EVA Kerbal
            if (FlightGlobals.ActiveVessel != null
                && FlightGlobals.ActiveVessel.FindPartModuleImplementing<KerbalEVA>() != null)
            {
                return;
            }

            Window = GUILayout.Window(98399854, Window, GUIDisplay, "OhScrap", GUILayout.Width(300));
        }

        private void GUIDisplay(int windowID)
        {
            //Grabs the vessels safety rating and shows the string associated with it.
            string safetyRatingString = vesselSafetyRating switch
            {
                10 => _OHS_08,
                9 => _OHS_08,
                8 => _OHS_11,
                7 => _OHS_11,
                6 => _OHS_10,
                5 => _OHS_10,
                4 => _OHS_11,
                3 => _OHS_11,
                2 => _OHS_12,
                1 => _OHS_12,
                0 => _OHS_13,
                _ => _OHS_16,
            };

            if (vesselSafetyRating == -1 || editorConstruct == null || editorConstruct.parts.Count() == 0)
            {
                if (HighLogic.LoadedSceneIsEditor || vesselSafetyRating == -1)
                {
                    GUILayout.Label(Localizer.Format(_OHS_15));
                    return;
                }
            }

            //GUILayout.Label(Localizer.Format("#OHS-16" + ": ") + vesselSafetyRating + " " + s);
            GUILayout.Label(Localizer.Format(_OHS_16, vesselSafetyRating, safetyRatingString));
            advancedDisplay = File.Exists(KSPUtil.ApplicationRootPath + "GameData/OhScrap/debug.txt");
            if (advancedDisplay)
            {
                GUILayout.Label(Localizer.Format(_OHS_17));
                GUILayout.Label(Localizer.Format(_OHS_18, failureMode));
                //GUILayout.Label("Chance of Failure in next " + sampleTime + ": " + displayFailureChance + "%");
                GUILayout.Label(Localizer.Format(_OHS_19, sampleTime, displayFailureChance));
            }

            if (worstPart != null)
            {
                //GUILayout.Label("Worst Part: " + worstPart.partInfo.title);
                GUILayout.Label(Localizer.Format(_OHS_20, worstPart.partInfo.title));

                //if (GUILayout.Button("Highlight Worst Part")) highlightWorstPart = !highlightWorstPart;
                if (GUILayout.Button(Localizer.Format(_OHS_21))) highlightWorstPart = !highlightWorstPart;
            }

            //if (GUILayout.Button("Close"))
            if (GUILayout.Button(Localizer.Format(_autoLOC_149410)))
            {
                display = false;
                ToggleWindow();
            }

            GUI.DragWindow();
        }

        private void ToggleWindow()
        {
            if (HighLogic.LoadedSceneIsEditor) editorWindow = display;
            else flightWindow = display;
        }

        private void OnDisable()
        {
            display = false;
            GameEvents.onGUIApplicationLauncherReady.Remove(GUIReady);
            GameEvents.onPartDie.Remove(OnPartDie);
            GameEvents.OnFlightGlobalsReady.Remove(OnFlightGlobalsReady);
            GameEvents.onVesselSituationChange.Remove(SituationChange);

            if (ToolbarButton == null)
            {
                return;
            }

            ApplicationLauncher.Instance.RemoveModApplication(ToolbarButton);
        }

        private void OnShowUI() // triggered on F2
        {
            visibleUI = true;
        }

        private void OnHideUI() // triggered on F2
        {
            visibleUI = false;
        }

        internal void OnDestroy()
        {
            GameEvents.onShowUI.Remove(OnHideUI);
            GameEvents.onHideUI.Remove(OnShowUI);
        }
    }
}