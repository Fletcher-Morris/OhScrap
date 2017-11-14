﻿using ScrapYard.Modules;
using ScrapYard;
using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;
using KSP.UI.Screens;
using System.Collections;

namespace Untitled_Part_Failure_Mod
{
    class BaseFailureModule : PartModule
    {
        bool ready = false;
        public float randomisation;
        public bool willFail = false;
        [KSPField(isPersistant = true, guiActive = false)]
        public bool launched = false;
        [KSPField(isPersistant = true, guiActive = false)]
        public bool hasFailed = false;
        [KSPField(isPersistant = true, guiActive = false)]
        public bool postMessage = true;
        [KSPField(isPersistant = true, guiActive = false)]
        public string failureType = "none";
        [KSPField(isPersistant = true, guiActive = false)]
        public int expectedLifetime = 2;
        [KSPField(isPersistant = true, guiActive = false)]
        public int counter = 0;
        public float actualLifetime;
        public ModuleSYPartTracker SYP;
        [KSPField(isPersistant = true, guiActive = false)]
        public float chanceOfFailure = 0.5f;
        [KSPField(isPersistant = true, guiActive = false)]
        public float baseChanceOfFailure = 0.01f;
        [KSPField(isPersistant = false, guiActive = true, guiName = "BaseFailure" ,guiActiveEditor = true, guiUnits = "%")]
        public int displayChance = 0;
        double failureTime = 0;
        public double maxTimeToFailure = 1800;
        public ModuleUPFMEvents UPFM;


        private void Start()
        {
            chanceOfFailure = baseChanceOfFailure;
            Overrides();
            ScrapYardEvents.OnSYTrackerUpdated.Add(OnSYTrackerUpdated);
            ScrapYardEvents.OnSYInventoryAppliedToVessel.Add(OnSYInventoryAppliedToVessel);
            UPFM = part.FindModuleImplementing<ModuleUPFMEvents>();
            UPFM.RefreshPart();
            if (launched || HighLogic.LoadedSceneIsEditor) Initialise();
            GameEvents.onLaunch.Add(OnLaunch);
        }

        private void OnSYInventoryAppliedToVessel()
        {
#if DEBUG
            Debug.Log("[UPFM]: ScrayYard Inventory Applied. Recalculating failure chance for "+SYP.ID+" "+ClassName);
# endif
            if(UPFMUtils.instance != null) UPFMUtils.instance.damagedParts.Remove(part);
            willFail = false;
            chanceOfFailure = baseChanceOfFailure;
            Initialise();
        }

        private void OnLaunch(EventReport data)
        {
            launched = true;
            Initialise();
            if (!HighLogic.CurrentGame.Parameters.CustomParams<UPFMSettings>().safetyRecover && displayChance < HighLogic.CurrentGame.Parameters.CustomParams<UPFMSettings>().safetyThreshold) return;
            PartModule dontRecover = part.FindModuleImplementing<DontRecoverMe>();
            if (dontRecover == null) return;
            part.RemoveModule(dontRecover);
#if DEBUG
            Debug.Log("[UPFM]: " + SYP.ID + "marked as recoverable");
#endif
        }

        private void OnSYTrackerUpdated(IEnumerable<InventoryPart> data)
        {
#if DEBUG
            Debug.Log("[UPFM]: ScrayYard Tracker updated. Recalculating failure chance for "+SYP.ID+" "+ClassName);
#endif
            willFail = false;
            chanceOfFailure = baseChanceOfFailure;
            Initialise();
            part.AddModule("DontRecoverMe");
        }

        public void Initialise()
        {
            SYP = part.FindModuleImplementing<ModuleSYPartTracker>();
            ready = SYP.ID != "";
            if (!ready) return;
            randomisation = UPFMUtils.instance.GetRandomisation(part);
            actualLifetime = UPFMUtils.instance.GetExpectedLifetime(part, expectedLifetime, ClassName);
            if (hasFailed)
            {
                UPFM.Events["RepairChecks"].active = true;
                UPFM.Events["ToggleHighlight"].active = true;
            }
            else
            {
                if (FailCheck(true) && !HighLogic.LoadedSceneIsEditor && launched)
                {
                    double timeToFailure = (maxTimeToFailure * (1 - chanceOfFailure)) * Randomiser.instance.NextDouble();
                    failureTime = Planetarium.GetUniversalTime() + timeToFailure;
                    willFail = true;
                    Debug.Log("[UPFM]: " + SYP.ID + " " + ClassName + " will attempt to fail in " + timeToFailure + " seconds");
#if !DEBUG
                    Debug.Log("[UPFM]: Chance of Failure was "+displayChance+"% (Generation "+ ScrapYardWrapper.GetBuildCount(part, ScrapYardWrapper.TrackType.NEW));
#endif
                }
            }
            displayChance = (int)(chanceOfFailure * 100);
            if (displayChance >= HighLogic.CurrentGame.Parameters.CustomParams<UPFMSettings>().safetyThreshold && UPFMUtils.instance != null)
            {
                if (UPFMUtils.instance.damagedParts.TryGetValue(part, out int i))
                {
                    if (i < displayChance)
                    {
                        UPFMUtils.instance.damagedParts.Remove(part);
                        UPFMUtils.instance.damagedParts.Add(part, displayChance);
                        if (HighLogic.LoadedSceneIsEditor) UPFMUtils.instance.display = true;
                    }
                }
                else
                {
                    UPFMUtils.instance.damagedParts.Add(part, displayChance);
                    if(HighLogic.LoadedSceneIsEditor) UPFMUtils.instance.display = true;
                }
            }
            if (UPFMUtils.instance != null && hasFailed)
            {
                if (!UPFMUtils.instance.brokenParts.ContainsKey(part)) UPFMUtils.instance.brokenParts.Add(part, displayChance);
                UPFMUtils.instance.damagedParts.Remove(part);
            }
        }
        protected virtual void Overrides() { }

        protected virtual void FailPart() { }

        public virtual void RepairPart() { }

        protected virtual bool FailureAllowed() { return false; }

        private void FixedUpdate()
        {
            if (!ready) Initialise();
            if (HighLogic.LoadedSceneIsEditor) return;
            if (KRASHWrapper.simulationActive()) return;
            if (!FailureAllowed()) return;
            if (hasFailed)
            {
                FailPart();
                UPFM.SetFailedHighlight();
                if (postMessage)
                {
                    PostFailureMessage();
                    postMessage = false;
                    UPFM.Events["ToggleHighlight"].active = true;
                    UPFM.highlight = true;
                }
                return;
            }
            if (!willFail)
            {
                return;
            }
            if (Planetarium.GetUniversalTime() < failureTime) return;
            hasFailed = true;
            if (UPFMUtils.instance != null)
            {
                if (!UPFMUtils.instance.brokenParts.ContainsKey(part)) UPFMUtils.instance.brokenParts.Add(part, displayChance);
                UPFMUtils.instance.damagedParts.Remove(part);
            }
            UPFM.Events["RepairChecks"].active = true;
            if (FailCheck(false))
            {
                part.AddModule("Broken");
                Debug.Log("[UPFM]: " + SYP.ID + "is too badly damaged to be salvaged");
            }
        }
        

        public bool FailCheck(bool recalcChance)
        {
            if (SYP.TimesRecovered == 0) chanceOfFailure = baseChanceOfFailure + randomisation;
            else chanceOfFailure = (SYP.TimesRecovered / actualLifetime)+randomisation;
#if DEBUG
            if (part != null) Debug.Log("[UPFM]: Chances of " + SYP.ID +" "+ moduleName +" failing calculated to be " + chanceOfFailure * 100 + "%");
#endif
            if (Randomiser.instance.NextDouble() < chanceOfFailure) return true;
            return false;
        }

        void PostFailureMessage()
        {
            StringBuilder msg = new StringBuilder();
            msg.AppendLine(part.vessel.vesselName);
            msg.AppendLine("");
            msg.AppendLine(part.name + " has suffered a " + failureType);
            msg.AppendLine("");
            if (part.FindModuleImplementing<Broken>() != null) msg.AppendLine("The part is damaged beyond repair");
            else msg.AppendLine("Chance of a successful repair is " + (100 - displayChance)+"%");
            MessageSystem.Message m = new MessageSystem.Message("UPFM", msg.ToString(), MessageSystemButton.MessageButtonColor.ORANGE,MessageSystemButton.ButtonIcons.ALERT);
            MessageSystem.Instance.AddMessage(m);
        }

        private void OnDestroy()
        {
            GameEvents.onLaunch.Remove(OnLaunch);
            if (ScrapYardEvents.OnSYTrackerUpdated != null) ScrapYardEvents.OnSYTrackerUpdated.Remove(OnSYTrackerUpdated);
            if (ScrapYardEvents.OnSYInventoryAppliedToVessel != null) ScrapYardEvents.OnSYInventoryAppliedToVessel.Remove(OnSYInventoryAppliedToVessel);
        }
    }
}
