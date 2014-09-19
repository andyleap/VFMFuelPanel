using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace FuelPanel
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    class FuelPanelManager : MonoBehaviour
	{
        public bool check = false;

        List<Pump> Pumps = new List<Pump>();

        List<PumpNetwork> PumpNetworks = new List<PumpNetwork>();

        public void Awake()
        {
            DontDestroyOnLoad(this);

            print("FuelPanel v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(4) + "Awake");

            GameEvents.onPartPack.Add(this.onPartPack);
            GameEvents.onPartUnpack.Add(this.onPartUnpack);
        }

        public void Update()
        {
            

            if (Settings.Instance.UpdateCheck && !check && MessageSystem.Ready)
            {
                check = true;
                /*KSVersionCheck.Check.CheckVersion(57, latest =>
                {
                    if (latest.friendly_version != Assembly.GetExecutingAssembly().GetName().Version.ToString(3))
                    {
                        MessageSystem.Instance.AddMessage(new MessageSystem.Message(
                            "New FuelPanel Version",
                            "There is a new FuelPanel Version Available\nCurrent Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3) + "\nNew Version: " + latest.friendly_version + "\nChanges:\n" + latest.changelog + "\nGo to http://beta.kerbalstuff.com/mod/57 to download",
                            MessageSystemButton.MessageButtonColor.ORANGE,
                            MessageSystemButton.ButtonIcons.ALERT
                            ));

                    }
                });*/
            }

            

            foreach(Pump p in Pumps)
            {


            }

        }

        public void onPartPack(Part part)
        {
            Pump p = Pumps.FirstOrDefault(pump => pump.part == part);
            if (p != null)
            {
                p.partID = part.uid;
                p.vesselID = part.vessel.id;
                p.part = null;
            }
        }

        public void onPartUnpack(Part part)
        {
            Pump p = Pumps.FirstOrDefault(pump => pump.partID == part.uid && pump.vesselID == part.vessel.id);
            if(p != null)
            {
                p.part = part;
                PumpNetwork network = PumpNetwork.FindNetwork(part.vessel);

                if (network != null)
                {


                }

            }
        }
	}
}
