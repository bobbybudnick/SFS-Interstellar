using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using OpenResourceSystem;

namespace FNPlugin
{
    class AntimatterStorageTank : FNResourceSuppliableModule
    {
        //Persistent True
        [KSPField(isPersistant = true)]
        public double chargestatus = 1000.0f;

        //Persistent False
        [KSPField(isPersistant = false)]
        public float chargeNeeded = 100f;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Charge")]
        public string chargeStatusStr;
        [KSPField(isPersistant = false, guiActive = true, guiName = "Status")]
        public string statusStr;
        [KSPField(isPersistant = false, guiActiveEditor = true, guiActive = true, guiName = "Current")]
        public string capacityStr;
        [KSPField(isPersistant = false, guiActiveEditor = true, guiActive = true, guiName = "Maximum")]
        public string maxAmountStr;

        bool charging = false;
        bool should_charge = false;
        double explosion_time = 0.35f;
        bool exploding = false;
        double explosion_size = 5000;
        double cur_explosion_size = 0;
        double current_antimatter = 0;
        int explode_counter = 0;
        GameObject lightGameObject;
        protected PartResource antimatter;


        [KSPEvent(guiActive = true, guiName = "Start Charging", active = true)]
        public void StartCharge()
        {
            should_charge = true;
        }

        [KSPEvent(guiActive = true, guiName = "Stop Charging", active = true)]
        public void StopCharge()
        {
            should_charge = false;
        }

        public void doExplode()
        {
            if (current_antimatter <= 0.1f) return;

            lightGameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lightGameObject.GetComponent<Collider>().enabled = false;
            lightGameObject.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            lightGameObject.AddComponent<Light>();
            var renderer = lightGameObject.GetComponent<Renderer>();
            renderer.material.shader = Shader.Find("Unlit/Transparent");
            renderer.material.mainTexture = GameDatabase.Instance.GetTexture("WarpPlugin/ParticleFX/explode", false);
            renderer.material.color = new Color(Color.white.r, Color.white.g, Color.white.b, 0.9f);
            Light light = lightGameObject.GetComponent<Light>();
            lightGameObject.transform.position = part.transform.position;
            light.type = LightType.Point;
            light.color = Color.white;
            light.range = 100f;
            light.intensity = 500000.0f;
            light.renderMode = LightRenderMode.ForcePixel;
            //Destroy (lightGameObject.collider, 0.25f);
            Destroy(lightGameObject, 0.25f);

            //bool exist_parts_to_explode = true;
            //Part part_to_explode = null;
            exploding = true;
        }

        public override void OnStart(PartModule.StartState state)
        {
            antimatter = part.Resources[InterstellarResourcesConfiguration.Instance.Antimatter];

            if (state == StartState.Editor) return;
            //this.part.force_activate();

            // charge if there is any antimatter
            should_charge = antimatter.amount > 0;

            this.enabled = true;
        }

        public override void OnUpdate()
        {
            Events["StartCharge"].active = current_antimatter <= 0.1 && !should_charge;
            Events["StopCharge"].active = current_antimatter <= 0.1 && should_charge;
            chargeStatusStr = chargestatus.ToString("0.0") + "/" + GameConstants.MAX_ANTIMATTER_TANK_STORED_CHARGE.ToString("0.0");

            if (chargestatus <= 60 && !charging && current_antimatter > 0.1)
                ScreenMessages.PostScreenMessage("Warning!: Antimatter storage unpowered, tank explosion in: " + chargestatus.ToString("0") + "s", 1.0f, ScreenMessageStyle.UPPER_CENTER);

            if (current_antimatter > 0.1)
            {
                if (charging)
                    statusStr = "Charging.";
                else
                    statusStr = "Discharging!";
            }
            else
            {
                if (should_charge)
                    statusStr = "Charging.";
                else
                    statusStr = "No Power Required.";
            }

            UpdateAmounts();
        }

        public void Update()
        {
            UpdateAmounts();
        }

        private void UpdateAmounts()
        {
            capacityStr = formatMassStr(antimatter.amount);
            maxAmountStr = formatMassStr(antimatter.maxAmount);
        }

        public override void OnFixedUpdate()
        {
            MaintainContainment();

            ExplodeContainer();
        }

        private void MaintainContainment()
        {
            if (antimatter == null) return;

            float mult = 1;
            current_antimatter = antimatter.amount;
            explosion_size = Math.Sqrt(current_antimatter) * 5.0;

            if (chargestatus > 0 && (current_antimatter > 0.00001 * antimatter.maxAmount))
                chargestatus -= 1.0f * TimeWarp.fixedDeltaTime;

            if (chargestatus >= GameConstants.MAX_ANTIMATTER_TANK_STORED_CHARGE)
                mult = 0.5f;

            if (!should_charge && current_antimatter <= 0.00001 * antimatter.maxAmount) return;

            var powerRequest = mult * 2.0 * chargeNeeded / 1000.0 * TimeWarp.fixedDeltaTime;

            double charge_to_add = CheatOptions.InfiniteElectricity
                ? powerRequest 
                : consumeFNResource(powerRequest, FNResourceManager.FNRESOURCE_MEGAJOULES) * 1000.0f / chargeNeeded;

            chargestatus += charge_to_add;

            if (charge_to_add < 2f * TimeWarp.fixedDeltaTime)
            {
                float more_charge_to_add = ORSHelper.fixedRequestResource(part, FNResourceManager.STOCK_RESOURCE_ELECTRICCHARGE, mult * 2 * chargeNeeded * TimeWarp.fixedDeltaTime) / chargeNeeded;
                charge_to_add += more_charge_to_add;
                chargestatus += more_charge_to_add;
            }

            if (charge_to_add >= 1f * TimeWarp.fixedDeltaTime)
                charging = true;
            else
            {
                charging = false;
                if (TimeWarp.CurrentRateIndex > 3 && (current_antimatter > 0.00001 * antimatter.maxAmount))
                {
                    TimeWarp.SetRate(3, true);
                    ScreenMessages.PostScreenMessage("Cannot Time Warp faster than 50x while Antimatter Tank is Unpowered", 1.0f, ScreenMessageStyle.UPPER_CENTER);
                }
            }
            //print (chargestatus);
            if (chargestatus <= 0)
            {
                chargestatus = 0;
                if (current_antimatter > 0.00001 * antimatter.maxAmount)
                {
                    explode_counter++;
                    if (explode_counter > 5)
                        doExplode();
                }
            }
            else
                explode_counter = 0;

            if (chargestatus > GameConstants.MAX_ANTIMATTER_TANK_STORED_CHARGE)
                chargestatus = GameConstants.MAX_ANTIMATTER_TANK_STORED_CHARGE;
        }

        private void ExplodeContainer()
        {
            if (!exploding || lightGameObject == null) return;

            if (Math.Sqrt(cur_explosion_size) > explosion_size)
            {
                lightGameObject.GetComponent<Collider>().enabled = false;
                //Destroy (lightGameObject);
            }

            cur_explosion_size += TimeWarp.fixedDeltaTime * explosion_size * explosion_size / explosion_time;
            lightGameObject.transform.localScale = new Vector3(Mathf.Sqrt((float)cur_explosion_size), Mathf.Sqrt((float)cur_explosion_size), Mathf.Sqrt((float)cur_explosion_size));
            lightGameObject.GetComponent<Light>().range = Mathf.Sqrt((float)cur_explosion_size) * 15f;

            if (Math.Sqrt(cur_explosion_size) <= explosion_size) return;

            TimeWarp.SetRate(0, true);
            vessel.GoOffRails();

            Vessel[] list_of_vessels_to_explode = FlightGlobals.Vessels.ToArray();
            foreach (Vessel vess_to_explode in list_of_vessels_to_explode)
            {
                if (Vector3d.Distance(vess_to_explode.transform.position, vessel.transform.position) > explosion_size) continue;

                if (vess_to_explode.packed) continue;

                Part[] parts_to_explode = vess_to_explode.Parts.ToArray();
                foreach (Part part_to_explode in parts_to_explode)
                {
                    if (part_to_explode != null)
                        part_to_explode.explode();
                }
            }

            Part[] explode_parts = vessel.Parts.ToArray();
            foreach (Part explode_part in explode_parts)
            {
                if (explode_part != vessel.rootPart && explode_part != this.part)
                    explode_part.explode();
            }
            vessel.rootPart.explode();
            this.part.explode();
            //	this.part.explode ();
            //	vessel.rootPart.explode ();
        }

        public override string GetInfo()
        {
            return "Maximum Power Requirements: " + (chargeNeeded * 2).ToString("0") + " KW\nMinimum Power Requirements: " + chargeNeeded.ToString("0") + " KW";
        }

        public override string getResourceManagerDisplayName()
        {
            return "Antimatter Storage Tank";
        }

        public override int getPowerPriority()
        {
            return 1;
        }

        protected string formatMassStr(double amount)
        {
            if (amount >= 1000)
                return (amount / 1000).ToString("0.0000000") + " g";
            else if (amount >= 1)
                return (amount).ToString("0.0000000") + " mg";
            else if (amount >= 1e-3)
                return (amount * 1e3).ToString("0.000000") + " ug";
            else if (amount > 1e-6)
                return (amount * 1e6).ToString("0.0000000") + " ng";
            else
                return (amount * 1e9).ToString("0.0000000") + " pg";
        }
    }

}

