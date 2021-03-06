﻿using OpenResourceSystem;
using System;
using UnityEngine;

namespace FNPlugin  
{
    class AtmosphericIntake : PartModule
    {
        //protected Vector3 _intake_direction;
        protected PartResourceDefinition _resourceAtmosphere;

        [KSPField(guiName = "Intake Speed", isPersistant = false, guiActive = true, guiFormat = "F3")]
        protected float _intake_speed;
        [KSPField(guiName = "Atmosphere Flow", guiUnits = "U", guiFormat = "F3", isPersistant = false, guiActive = false)]
        public double airFlow;
        [KSPField(guiName = "Atmossphere Speed", guiUnits = "M/s", guiFormat = "F3", isPersistant = false, guiActive = false)]
        public double airSpeed;
        [KSPField(guiName = "Air This Update", isPersistant = false, guiActive = true, guiFormat ="F6")]
        public double airThisUpdate;
        [KSPField(guiName = "intake Angle", isPersistant = false, guiActive = false)]
        public float intakeAngle = 0;

        [KSPField(guiName = "aoaThreshold", isPersistant = false, guiActive = false, guiActiveEditor = false)]
        public float aoaThreshold = 0.1f;
        [KSPField(isPersistant = false, guiName = "Area", guiActiveEditor = false, guiActive = false)]
        public double area = 0.01f;
        [KSPField(isPersistant = false)]
        public string intakeTransformName;
        [KSPField(isPersistant = false, guiName = "maxIntakeSpeed", guiActive = false, guiActiveEditor = false)]
        public float maxIntakeSpeed = 100;
        [KSPField(isPersistant = false, guiName = "unitScalar", guiActive = false, guiActiveEditor = false)]
        public double unitScalar = 0.2f;
		//[KSPField(isPersistant = false, guiName = "useIntakeCompensation", guiActiveEditor = false)]
		//public bool useIntakeCompensation = true;
        [KSPField(isPersistant = false, guiName = "storesResource", guiActiveEditor = true)]
        public bool storesResource = false;
        [KSPField(isPersistant = false, guiName = "Intake Exposure", guiActiveEditor = false, guiActive = false)]
        public double intakeExposure = 0;

        private float jetTechBonusPercentage;

        public override void OnStart(PartModule.StartState state)
        {
            Transform intakeTransform = part.FindModelTransform(intakeTransformName);
            if (intakeTransform == null)
                Debug.Log("[KSPI] AtmosphericIntake unable to get intake transform for " + part.name);

            //_intake_direction = intakeTransform != null ? intakeTransform.forward.normalized : Vector3.forward;
            _resourceAtmosphere = PartResourceLibrary.Instance.GetDefinition(InterstellarResourcesConfiguration.Instance.IntakeAtmosphere);

            // ToDo: connect with atmospheric intake to readout updated area
            // ToDo: change density of air to 

            _intake_speed = maxIntakeSpeed;

            bool hasJetUpgradeTech0 = PluginHelper.HasTechRequirementOrEmpty(PluginHelper.JetUpgradeTech0);
            bool hasJetUpgradeTech1 = PluginHelper.HasTechRequirementOrEmpty(PluginHelper.JetUpgradeTech1);
            bool hasJetUpgradeTech2 = PluginHelper.HasTechRequirementOrEmpty(PluginHelper.JetUpgradeTech2);
            bool hasJetUpgradeTech3 = PluginHelper.HasTechRequirementOrEmpty(PluginHelper.JetUpgradeTech3);

            var jetTechBonus = Convert.ToInt32(hasJetUpgradeTech0) + 1.2f * Convert.ToInt32(hasJetUpgradeTech1) + 1.44f * Convert.ToInt32(hasJetUpgradeTech2) + 1.728f * Convert.ToInt32(hasJetUpgradeTech3);
            jetTechBonusPercentage = 1 + (jetTechBonus / 10.736f);
        }

        public void FixedUpdate()
        {
            if (vessel == null)
                return;

            airSpeed = vessel.speed + _intake_speed;
            intakeExposure = (airSpeed * unitScalar) + _intake_speed;
            intakeExposure *= area * unitScalar * jetTechBonusPercentage;
            airFlow = vessel.atmDensity * intakeExposure / _resourceAtmosphere.density ;
            airThisUpdate = airFlow * TimeWarp.fixedDeltaTime;

            if (!storesResource)
            {
                foreach (PartResource resource in part.Resources)
                {
                    if (resource.resourceName != _resourceAtmosphere.name)
                        continue;

                    airThisUpdate = airThisUpdate >= 0
                        ? (airThisUpdate <= resource.maxAmount
                            ? airThisUpdate
                            : resource.maxAmount)
                        : 0;
                    resource.amount = airThisUpdate;
                    break;
                }
            }
            else
            {
                //part.ImprovedRequestResource(_resourceAtmosphere.name, -airThisUpdate);
                part.RequestResource(_resourceAtmosphere.name, -airThisUpdate);
            }

        }

    }
}
