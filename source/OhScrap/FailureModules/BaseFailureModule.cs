using KSP.Localization;
using ScrapYard;
using ScrapYard.Modules;
using System.Collections.Generic;
using UnityEngine;

namespace OhScrap
{
    //This is the generic Failure Module which all other modules inherit.
    //BaseFailureModule will never be attached directly to a part
    //but this handles the stuff that all modules need (like "will I fail" etc)
    internal class BaseFailureModule : PartModule
    {
        public bool ready = false;
        public bool willFail = false;
        [KSPField(isPersistant = true, guiActive = false)]
        public bool launched = false;
        [KSPField(isPersistant = true, guiActive = false)]
        public bool endOfLife = false;
        [KSPField(isPersistant = true, guiActive = false)]
        public bool hasFailed = false;
        [KSPField(isPersistant = true, guiActive = false)]
        public string failureType = "none";
        [KSPField(isPersistant = true, guiActive = false)]
        public int expectedLifetime = 2;
        public ModuleSYPartTracker SYP;
        [KSPField(isPersistant = true, guiActive = false)]
        public float chanceOfFailure = 0.1f;
        [KSPField(isPersistant = true, guiActive = false)]
        public float baseChanceOfFailure = 0.1f;
        [KSPField(isPersistant = true, guiActive = false)]
        public int numberOfRepairs = 0;
        //[KSPField(isPersistant = false, guiActive = false, guiName = "BaseFailure", guiActiveEditor = false, guiUnits = "%")]
        [KSPField(isPersistant = false, guiActive = false, guiName = "#OHS-BFM-displayChance", guiActiveEditor = false, guiUnits = "%")]
        public int displayChance = 100;
        //[KSPField(isPersistant = false, guiActive = true, guiName = "Base Safety Rating", guiActiveEditor = true)]
        [KSPField(isPersistant = false, guiActive = true, guiName = "#OHS-BFM-safetyRating", guiActiveEditor = true)]
        public int safetyRating = -1;
        public ModuleUPFMEvents OhScrap;
        public bool remoteRepairable = false;
        public bool isSRB = false;
        public bool excluded = false;

        //#if DEBUG
        //[KSPEvent(active = true, guiActive = true, guiActiveUnfocused = true, unfocusedRange = 5.0f, externalToEVAOnly = false, guiName = "Force Failure (DEBUG)")]
        [KSPEvent(active = true, guiActive = true, guiActiveUnfocused = true, unfocusedRange = 5.0f, externalToEVAOnly = false, guiName = "#OHS-BFM-ForceFailure")]
        public void ForceFailure()
        {
            if (HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu)
            {
                if (!ready)
                {
                    launched = true;
                    Initialize();
                }

                FailPart();
                hasFailed = true;
            }
        }
        //[KSPEvent(active = true, guiActive = true, guiActiveUnfocused = true, unfocusedRange = 5.0f, externalToEVAOnly = false, guiName = "Force Repair(DEBUG)")]
        [KSPEvent(active = true, guiActive = true, guiActiveUnfocused = true, unfocusedRange = 5.0f, externalToEVAOnly = false, guiName = "#OHS-BFM-ForceRepair")]
        public void ForcedRepair()
        {
            OhScrap.Events["RepairChecks"].active = HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu;
            OhScrap.Events["ToggleHighlight"].active = HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu;
            OhScrap.Events["RepairChecks"].guiActive = HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu;
            OhScrap.Events["ToggleHighlight"].guiActive = HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu;

            if (HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu)
            {
                //OhScrap.Events["RepairChecks"].active = true;
                //OhScrap.Events["ToggleHighlight"].active = true;
                hasFailed = false;
                willFail = false;
                RepairPart();

                numberOfRepairs++;

                Debug.Log(SYP.ID + " " + moduleName + " was successfully repaired");

                part.highlightType = Part.HighlightType.OnMouseOver;
                part.SetHighlightColor(Color.green);
                part.SetHighlight(false, false);
            }
            //else
            //{
            //    OhScrap.Events["RepairChecks"].active = false;
            //    OhScrap.Events["ToggleHighlight"].active = false;
            //    OhScrap.Events["RepairChecks"].guiActive = false;
            //    OhScrap.Events["ToggleHighlight"].guiActive = false;
            //}
        }
        // #endif
        private void Start()
        {
            // #if DEBUG
            //if (HighLogic.CurrentGame.Parameters.CustomParams<OhScrapSettings>().logging)
            //{

            //    Fields["displayChance"].guiActive = true;
            //    Fields["displayChance"].guiActiveEditor = true;
            //    Fields["safetyRating"].guiActive = true;
            //} else
            //{

            //    Fields["displayChance"].guiActive = false;
            //    Fields["displayChance"].guiActiveEditor = true;
            //    Fields["safetyRating"].guiActive = true;
            //}    
            // #endif

            Fields["displayChance"].guiActive = HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu;
            Fields["displayChance"].guiActiveEditor = HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu;
            Fields["safetyRating"].guiActive = HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu;

            if (HighLogic.LoadedSceneIsEditor) hasFailed = false;

            //find the ScrapYard Module straight away, as we can't do any calculations without it.
            SYP = part.FindModuleImplementing<ModuleSYPartTracker>();
            chanceOfFailure = baseChanceOfFailure;

            //overrides are defined in each failure Module - stuff that the generic module can't handle.
            Overrides();

            //listen to ScrapYard Events so we can recalculate when needed
            ScrapYardEvents.OnSYTrackerUpdated.Add(OnSYTrackerUpdated);
            ScrapYardEvents.OnSYInventoryAppliedToVessel.Add(OnSYInventoryAppliedToVessel);
            ScrapYardEvents.OnSYInventoryAppliedToPart.Add(OnSYInventoryAppliedToPart);
            ScrapYardEvents.OnSYInventoryChanged.Add(OnSYInventoryChanged);
            OhScrap = part.FindModuleImplementing<ModuleUPFMEvents>();

            //refresh part if we are in the editor and parts never been used before (just returns if not)
            OhScrap.RefreshPart();

            //Initialize the Failure Module.
            GameEvents.onLaunch.Add(OnLaunch);
            if (launched || HighLogic.LoadedSceneIsEditor) Initialize();
        }

        private void OnLaunch(EventReport data)
        {
            if (FlightGlobals.ActiveVessel.speed > 8) return;
            ActivateFailures();
        }

        private void OnSYInventoryAppliedToPart(Part p)
        {
            if (p != part) return;
            willFail = false;
            chanceOfFailure = baseChanceOfFailure;
            Initialize();
        }

        private void OnSYInventoryChanged(InventoryPart data0, bool data1)
        {
            willFail = false;
            chanceOfFailure = baseChanceOfFailure;
            Initialize();
        }

        // if SY applies inventory we reset the module as it could be a whole new part now.
        private void OnSYInventoryAppliedToVessel()
        {
            //#if DEBUG
            Debug.Log("[UPFM]: ScrayYard Inventory Applied. Recalculating failure chance for " + SYP.ID + " " + ClassName);
            //#endif
            willFail = false;
            chanceOfFailure = baseChanceOfFailure;
            Initialize();
        }

        //likewise for when the SYTracker is updated (usually on VesselRollout). Start() fires before ScrapYard applies the inventory in the flight scene.
        private void OnSYTrackerUpdated(IEnumerable<InventoryPart> data)
        {
            Debug.Log("[UPFM]: ScrayYard Tracker updated. Recalculating failure chance for " + SYP.ID + " " + ClassName);

            willFail = false;
            chanceOfFailure = baseChanceOfFailure;
            Initialize();
        }

        private void ActivateFailures()
        {
            if (KRASHWrapper.simulationActive())
            {
                return;
            }

            launched = true;
            Initialize();
            Utils.instance.testedParts.Add(SYP.ID);

            if (HighLogic.LoadedScene == GameScenes.FLIGHT
                && isSRB
                && FailureAllowed()
                && Utils.instance._randomiser.NextDouble() < chanceOfFailure)
            {
                InvokeRepeating("FailPart", 0.01f, 0.01f);
            }
        }

        public void OnUpdate()
        {
            Events["ForceFailure"].guiName = Localizer.Format("#OHS-BFM-ForceFailure", moduleName);
            Events["ForcedRepair"].guiName = Localizer.Format("#OHS-BFM-ForceRepair", moduleName);
            Events["ForceFailure"].active = HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu;
            Events["ForcedRepair"].active = HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu;
            Events["ForceFailure"].guiActive = HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu;
            Events["ForcedRepair"].guiActive = HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu;

            base.OnUpdate();
        }

        // This is where we "initialize" the failure module and get everything ready
        public void Initialize()
        {
            // #if DEBUG
            //if (HighLogic.CurrentGame.Parameters.CustomParams<DebugSettings>().debugMenu)
            //{

            //Events["ForceFailure"].guiName = moduleName + "Force Failure (DEBUG)";
            //Events["ForcedRepair"].guiName = moduleName + "Force Repair (DEBUG)";
            Events["ForceFailure"].guiName = Localizer.Format("#OHS-BFM-ForceFailure", moduleName);
            Events["ForcedRepair"].guiName = Localizer.Format("#OHS-BFM-ForceRepair", moduleName);

            //    Events["ForceFailure"].active = true;
            //    Events["ForcedRepair"].active = true;
            //    Events["ForceFailure"].guiActive = true;
            //    Events["ForcedRepair"].guiActive = true;
            //}
            //else
            //{
            //    Events["ForceFailure"].guiName = "";
            //    Events["ForcedRepair"].guiName = "";
            //    Events["ForceFailure"].active = false;
            //    Events["ForcedRepair"].active = false;
            //    Events["ForceFailure"].guiActive = false;
            //    Events["ForcedRepair"].guiActive = false;
            //}
            // #endif
            //ScrapYard isn't always ready when OhScrap is so we check to see if it's returning an ID yet. If not, return and wait until it does.
            ready = SYP.ID != 0 && Utils.instance.ready;

            if (!ready)
            {
                return;
            }

            if (Utils.instance.testedParts.Contains(SYP.ID))
            {
                part.FindModuleImplementing<ModuleUPFMEvents>().tested = true;
            }

            OhScrap.generation = Utils.instance.GetGeneration(SYP.ID, part);
            chanceOfFailure = baseChanceOfFailure;

            if (SYP.TimesRecovered == 0 || !Utils.instance.testedParts.Contains(SYP.ID))
            {
                chanceOfFailure = CalculateInitialFailureRate();
            }
            else
            {
                chanceOfFailure = CalculateInitialFailureRate() * (SYP.TimesRecovered / (float)expectedLifetime);
            }

            if (chanceOfFailure < Utils.instance.minimumFailureChance)
            {
                chanceOfFailure = Utils.instance.minimumFailureChance;
            }

            if (SYP.TimesRecovered > expectedLifetime)
            {
                float endOfLifeBonus = (float)expectedLifetime / SYP.TimesRecovered;
                chanceOfFailure += (1 - endOfLifeBonus) / 10;
            }

            //if the part has already failed turn the repair and highlight events on.
            if (hasFailed)
            {
                OhScrap.Events["RepairChecks"].active = true;
                OhScrap.Events["ToggleHighlight"].active = true;
            }

            displayChance = (int)(chanceOfFailure * 100);
            //this compares the actual failure rate to the safety threshold and returns a safety calc based on how far below the safety threshold the actual failure rate is.
            //This is what the player actually sees when determining if a part is "failing" or not.

            if (!isSRB)
            {
                if (chanceOfFailure <= baseChanceOfFailure / 10) safetyRating = 10;
                else if (chanceOfFailure < baseChanceOfFailure / 10 * 2) safetyRating = 9;
                else if (chanceOfFailure < baseChanceOfFailure / 10 * 3) safetyRating = 8;
                else if (chanceOfFailure < baseChanceOfFailure / 10 * 4) safetyRating = 7;
                else if (chanceOfFailure < baseChanceOfFailure / 10 * 5) safetyRating = 6;
                else if (chanceOfFailure < baseChanceOfFailure / 10 * 6) safetyRating = 5;
                else if (chanceOfFailure < baseChanceOfFailure / 10 * 7) safetyRating = 4;
                else if (chanceOfFailure < baseChanceOfFailure / 10 * 8) safetyRating = 3;
                else if (chanceOfFailure < baseChanceOfFailure / 10 * 9) safetyRating = 2;
                else safetyRating = 1;

                if (hasFailed)
                {
                    part.FindModuleImplementing<ModuleUPFMEvents>().SetFailedHighlight();
                }

                ready = true;
            }
            else
            {
                if (chanceOfFailure <= baseChanceOfFailure / 10) safetyRating = 10;
                else if (chanceOfFailure < baseChanceOfFailure / 9) safetyRating = 9;
                else if (chanceOfFailure < baseChanceOfFailure / 8) safetyRating = 8;
                else if (chanceOfFailure < baseChanceOfFailure / 7) safetyRating = 7;
                else if (chanceOfFailure < baseChanceOfFailure / 6) safetyRating = 6;
                else if (chanceOfFailure < baseChanceOfFailure / 5) safetyRating = 5;
                else if (chanceOfFailure < baseChanceOfFailure / 4) safetyRating = 4;
                else if (chanceOfFailure < baseChanceOfFailure / 3) safetyRating = 3;
                else if (chanceOfFailure < baseChanceOfFailure / 2) safetyRating = 2;
                else safetyRating = 1;
            }
        }

        private float CalculateInitialFailureRate()
        {
            int generation = OhScrap.generation;
            if (generation > 10) generation = 10;
            if (isSRB) return baseChanceOfFailure / generation;
            return baseChanceOfFailure + 0.01f - (generation * (baseChanceOfFailure / 10));
        }

        //These methods all are overridden by the failure modules

        //Overrides are things like the UI names, and specific things that we might want to be different for a module
        //For example engines fail after only 2 minutes instead of 30
        protected virtual void Overrides() { }
        //This actually makes the failure happen
        public virtual void FailPart()
        {

        }
        //this repairs the part.
        public virtual void RepairPart() { }
        //this should read from the Difficulty Settings.
        public virtual bool FailureAllowed() { return false; }

        private void FixedUpdate()
        {
            //If ScrapYard didn't return a sensible ID last time we checked, try again.
            if (!ready)
            {
                Initialize();
                return;
            }
            //OnLaunch doesn't fire for rovers, so we do a secondary check for whether the vessel is moving, and fire it manually if it is.
            if (!launched && FlightGlobals.ActiveVessel != null)
            {
                if (FlightGlobals.ActiveVessel.speed > 8 && !isSRB) ActivateFailures();
                return;
            }
            if (hasFailed)
            {
                OhScrap.Events["RepairChecks"].active = true;
                OhScrap.Events["ToggleHighlight"].active = true;
                FailPart();
            }
        }

        private void OnDisable()
        {
            GameEvents.onLaunch.Remove(OnLaunch);

            if (ScrapYardEvents.OnSYTrackerUpdated != null)
            {
                ScrapYardEvents.OnSYTrackerUpdated.Remove(OnSYTrackerUpdated);
            }

            if (ScrapYardEvents.OnSYInventoryAppliedToVessel != null)
            {
                ScrapYardEvents.OnSYInventoryAppliedToVessel.Remove(OnSYInventoryAppliedToVessel);
            }
        }
    }
}
