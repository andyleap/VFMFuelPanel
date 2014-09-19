using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResPumpPanel : PartModule
{
    public float TransferRate = 10f;

    private Rect WindowPos { get; set; }

    private Vector2 MasterScroll { get; set; }

    [KSPField(isPersistant = true)]
    public bool FuelPanel = false;

    [KSPField]
    private bool partResourcesByDockedVesselPulled;

    private uint activeGuiVessel;
    private int activeResource;
    private DateTime connectedVesselsNextCheck = DateTime.MinValue;

    [KSPField(isPersistant = true)]
    private bool transferRunning;

    private bool guiRegistered;
    private bool awakeRun;
    private Dictionary<int, List<PartResource>> transfersFrom;
    private Dictionary<int, List<PartResource>> transfersTo;
    private Dictionary<int, List<PartDef>> transfersFromDelayed;
    private Dictionary<int, List<PartDef>> transfersToDelayed;
    private Dictionary<int, double> fuelPulled;
    private Dictionary<uint, Dictionary<int, List<PartResource>>> partResourcesByDockedVessel;
    private Dictionary<uint, string> vesselNamesByRootID;
    private DateTime partResourcesByDockedVesselNextCheck;
    private DateTime loadTime;
    private List<Vessel> connectedVessels;
    private Part highlighted;

    [KSPEvent(guiActive = true, guiName = "Toggle Fuel Panel")]
    public void ToggleFuelPanel()
    {
        FuelPanel = !FuelPanel;
        if (!FuelPanel)
            return;
        part.SendEvent("DeactivateFuelPanel");
        FuelPanel = true;
    }

    [KSPEvent(name = "DeactivateFuelPanel")]
    public void DeactivateFuelPanel()
    {
        FuelPanel = false;
    }

    public override void OnUpdate()
    {
        if (!awakeRun)
            OnAwake();
        DateTime now;
        if (connectedVesselsNextCheck < DateTime.Now)
        {
            connectedVessels = GetAllNetworkedVessels();
            ResPumpPanel resPumpPanel = this;
            now = DateTime.Now;
            DateTime dateTime = now.AddSeconds(1.0);
            resPumpPanel.connectedVesselsNextCheck = dateTime;
        }
        if (vessel == FlightGlobals.ActiveVessel && vessel.IsControllable && FuelPanel)
        {
            if (!guiRegistered)
                RegisterGui();
            if (!partResourcesByDockedVesselPulled || partResourcesByDockedVesselNextCheck < DateTime.Now)
            {
                PullPartResourcesByDockedVesselPulled();
                ResPumpPanel resPumpPanel = this;
                now = DateTime.Now;
                DateTime dateTime = now.AddSeconds(5.0);
                resPumpPanel.partResourcesByDockedVesselNextCheck = dateTime;
                partResourcesByDockedVesselPulled = true;
            }
        }
        else if (guiRegistered)
            DeregisterGui();
        if (transfersFromDelayed != null)
        {
            var dictionary = new Dictionary<int, List<PartDef>>();
            foreach (var keyValuePair in transfersFromDelayed)
            {
                dictionary.Add(keyValuePair.Key, new List<PartDef>());
                if (!transfersFrom.ContainsKey(keyValuePair.Key))
                    transfersFrom.Add(keyValuePair.Key, new List<PartResource>());
                using (List<PartDef>.Enumerator enumerator = keyValuePair.Value.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        var resdef = enumerator.Current;
                        var resdefVessel =
                            FlightGlobals.Vessels.FirstOrDefault(v => v.id.ToString() == resdef.VesselID);
                        if (resdefVessel == null || !connectedVessels.Contains(resdefVessel)) continue;

                        var resdefPart =
                            resdefVessel.parts.FirstOrDefault(p => (int)p.flightID == (int)resdef.PartID);
                        if (resdefPart == null) continue;

                        transfersFrom[keyValuePair.Key].Add(resdefPart.Resources.Get(keyValuePair.Key));
                        dictionary[keyValuePair.Key].Add(resdef);
                    }
                }
            }
            foreach (KeyValuePair<int, List<PartDef>> keyValuePair in dictionary)
            {
                foreach (var partDef in keyValuePair.Value)
                    transfersFromDelayed[keyValuePair.Key].Remove(partDef);
                if (transfersFromDelayed[keyValuePair.Key].Count == 0)
                    transfersFromDelayed.Remove(keyValuePair.Key);
            }
            if (transfersFromDelayed.Count == 0)
                transfersFromDelayed = null;
            if (loadTime.AddSeconds(30.0) < DateTime.Now)
                transfersFromDelayed = null;
        }
        if (transfersToDelayed != null)
        {
            var dictionary = new Dictionary<int, List<PartDef>>();
            foreach (KeyValuePair<int, List<PartDef>> keyValuePair in transfersToDelayed)
            {
                dictionary.Add(keyValuePair.Key, new List<PartDef>());
                if (!transfersTo.ContainsKey(keyValuePair.Key))
                    transfersTo.Add(keyValuePair.Key, new List<PartResource>());
                using (List<PartDef>.Enumerator enumerator = keyValuePair.Value.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        var resdef = enumerator.Current;
                        var resdefVessel =
                            FlightGlobals.Vessels.FirstOrDefault(v => v.id.ToString() == resdef.VesselID);
                        if (resdefVessel == null || !connectedVessels.Contains(resdefVessel)) continue;

                        var resdefPart =
                            resdefVessel.parts.FirstOrDefault(p => (int)p.flightID == (int)resdef.PartID);
                        if (resdefPart == null) continue;

                        transfersTo[keyValuePair.Key].Add(resdefPart.Resources.Get(keyValuePair.Key));
                        dictionary[keyValuePair.Key].Add(resdef);
                    }
                }
            }
            foreach (KeyValuePair<int, List<PartDef>> keyValuePair in dictionary)
            {
                foreach (var partDef in keyValuePair.Value)
                    transfersToDelayed[keyValuePair.Key].Remove(partDef);
                if (transfersToDelayed[keyValuePair.Key].Count == 0)
                    transfersToDelayed.Remove(keyValuePair.Key);
            }
            if (transfersToDelayed.Count == 0)
                transfersToDelayed = null;
            if (loadTime.AddSeconds(30.0) < DateTime.Now)
                transfersToDelayed = null;
        }
        foreach (int key in transfersFrom.Keys)
        {
            if (transfersFrom.ContainsKey(key))
            {
                var transferResource = transfersFrom[key].Where(tr => connectedVessels.Contains(tr.part.vessel)).ToList();
                foreach (PartResource partResource in transferResource)
                {
                    transfersFrom[key].Remove(partResource);
                }
            }
            if (transfersTo.ContainsKey(key))
            {
                var transferResource = transfersTo[key].Where(tr => connectedVessels.Contains(tr.part.vessel)).ToList();

                foreach (PartResource partResource in transferResource)
                {
                    transfersTo[key].Remove(partResource);
                }
            }
        }
        if (transferRunning)
        {
            foreach (int index1 in transfersFrom.Keys)
            {
                if (transfersFrom.ContainsKey(index1) && transfersTo.ContainsKey(index1))
                {
                    if (!fuelPulled.ContainsKey(index1)) fuelPulled.Add(index1, 0.0);
                    double num1 = 0.0;
                    var dictionary1 = new Dictionary<PartResource, double>();
                    foreach (PartResource key in transfersTo[index1])
                    {
                        num1 += key.maxAmount - key.amount;
                        dictionary1.Add(key, key.maxAmount - key.amount);
                    }
                    double num2 = num1 - fuelPulled[index1];
                    int count1 = transfersFrom[index1].Count;
                    int count2 = transfersTo[index1].Count;
                    double num3 = num2 / count1;
                    if (num3 > TransferRate * (double)TimeWarp.deltaTime)
                        num3 = TransferRate * (double)TimeWarp.deltaTime;
                    foreach (PartResource partResource in transfersFrom[index1])
                    {
                        Dictionary<int, double> dictionary2;
                        int index2;
                        (dictionary2 = fuelPulled)[index2 = index1] = dictionary2[index2] + partResource.part.TransferResource(index1, -num3);
                    }
                    foreach (PartResource partResource in dictionary1.OrderBy(fc => fc.Value).Select(fc => fc.Key))
                    {
                        Dictionary<int, double> dictionary2;
                        int index2;
                        (dictionary2 = fuelPulled)[index2 = index1] = dictionary2[index2] + partResource.part.TransferResource(index1, fuelPulled[index1] / count2);
                        --count2;
                    }
                }
            }
        }
        base.OnUpdate();
    }

    private List<Vessel> GetAllNetworkedVessels()
    {
        var list = new List<Vessel> { vessel };
        var queue = new Queue<Vessel>();
        queue.Enqueue(vessel);
        while (queue.Count > 0)
        {
            /*foreach (
                ResPumpConnection resPumpConnection in
                    queue.Dequeue().parts.Select(p => p.GetComponent<ResPumpConnection>()).Where(con => con != null)
                )
            {
                if (resPumpConnection.ConnectedVessel == null || list.Contains(resPumpConnection.ConnectedVessel))
                    continue;

                list.Add(resPumpConnection.ConnectedVessel);
                queue.Enqueue(resPumpConnection.ConnectedVessel);
            }*/
        }
        return list;
    }

    private void PullPartResourcesByDockedVesselPulled()
    {
        partResourcesByDockedVessel.Clear();
        vesselNamesByRootID.Clear();
        foreach (Vessel connectedVessel in connectedVessels)
        {
            var queue1 = new Queue<Part>();
            var queue2 = new Queue<uint>();
            var list = new List<uint>();
            partResourcesByDockedVessel.Add(connectedVessel.rootPart.flightID,
                                            new Dictionary<int, List<PartResource>>());
            vesselNamesByRootID.Add(connectedVessel.rootPart.flightID, connectedVessel.vesselName);
            list.Add(connectedVessel.rootPart.flightID);
            queue1.Enqueue(connectedVessel.rootPart);
            queue2.Enqueue(connectedVessel.rootPart.flightID);
            while (queue1.Count > 0)
            {
                Part part1 = queue1.Dequeue();
                uint index = queue2.Dequeue();
                var component = part1.GetComponent<ModuleDockingNode>();
                if (component != null && component.vesselInfo != null && component.vesselInfo != null)
                {
                    index = component.vesselInfo.rootPartUId;
                    if (!partResourcesByDockedVessel.ContainsKey(component.vesselInfo.rootPartUId))
                    {
                        partResourcesByDockedVessel.Add(component.vesselInfo.rootPartUId,
                                                        new Dictionary<int, List<PartResource>>());
                        vesselNamesByRootID.Add(component.vesselInfo.rootPartUId, component.vesselInfo.name);
                    }
                }
                foreach (Part part2 in part1.children)
                {
                    if (!list.Contains(part2.flightID))
                    {
                        queue1.Enqueue(part2);
                        queue2.Enqueue(index);
                        list.Add(part2.flightID);
                    }
                }
                foreach (PartResource partResource in part1.Resources.list)
                {
                    if (!partResourcesByDockedVessel[index].ContainsKey(partResource.resourceName.GetHashCode()))
                        partResourcesByDockedVessel[index].Add(partResource.resourceName.GetHashCode(),
                                                               new List<PartResource>());
                    partResourcesByDockedVessel[index][partResource.resourceName.GetHashCode()].Add(partResource);
                }
            }
        }
    }

    // ResPumpPanel
    private void WindowGUI(int windowID)
    {
        bool flag = false;
        GUILayout.BeginHorizontal(new GUILayoutOption[]
            {
                GUILayout.Height(15f)
            });
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close", new GUILayoutOption[]
            {
                GUILayout.Width(100f)
            }))
        {
            FuelPanel = false;
        }
        GUILayout.EndHorizontal();
        Color color = GUI.color;
        MasterScroll = GUILayout.BeginScrollView(MasterScroll, new GUILayoutOption[]
            {
                GUILayout.Width(500f),
                GUILayout.Height(400f)
            });
        foreach (uint curVessel in partResourcesByDockedVessel.Keys)
        {
            if (GUILayout.Toggle(activeGuiVessel == curVessel, vesselNamesByRootID[curVessel] + " - Tanks",
                                 GUI.skin.button, new GUILayoutOption[]
                                     {
                                         GUILayout.ExpandWidth(true)
                                     }))
            {
                activeGuiVessel = curVessel;
                GUILayout.BeginVertical(GUI.skin.scrollView, new GUILayoutOption[0]);
                foreach (int resType in partResourcesByDockedVessel[curVessel].Keys)
                {
                    PartResourceDefinition definition = PartResourceLibrary.Instance.GetDefinition(resType);
                    if (definition.resourceTransferMode != 0)
                    {
                        GUILayout.BeginHorizontal(new GUILayoutOption[0]);
                        if (!transfersFrom.ContainsKey(resType))
                        {
                            transfersFrom.Add(resType, new List<PartResource>());
                        }
                        if (!transfersTo.ContainsKey(resType))
                        {
                            transfersTo.Add(resType, new List<PartResource>());
                        }
                        bool flag2 = GUILayout.Toggle(activeResource == resType, definition.name, GUI.skin.button,
                                                      new GUILayoutOption[]
                                                          {
                                                              GUILayout.ExpandWidth(true)
                                                          });
                        int num =
                            transfersTo[resType].Count(
                                res => partResourcesByDockedVessel[curVessel][resType].Contains(res));
                        int num2 =
                            transfersFrom[resType].Count(
                                res => partResourcesByDockedVessel[curVessel][resType].Contains(res));
                        int count = partResourcesByDockedVessel[curVessel][resType].Count;
                        if (num == count)
                        {
                            if (!GUILayout.Toggle(true, "In", GUI.skin.button, new GUILayoutOption[]
                                {
                                    GUILayout.Width(75f)
                                }))
                            {
                                (
                                    from res in transfersTo[resType]
                                    where partResourcesByDockedVessel[curVessel][resType].Contains(res)
                                    select res).ToList<PartResource>().ForEach(
                                        res => transfersTo[resType].Remove(res));
                            }
                        }
                        else
                        {
                            if (num > 0)
                            {
                                GUI.color = Color.gray;
                            }
                            if (GUILayout.Toggle(false, "In", GUI.skin.button, new GUILayoutOption[]
                                {
                                    GUILayout.Width(75f)
                                }))
                            {
                                (
                                    from res in partResourcesByDockedVessel[curVessel][resType]
                                    where !transfersTo[resType].Contains(res)
                                    select res).ToList<PartResource>().ForEach(res => transfersTo[resType].Add(res));
                                (
                                    from res in transfersFrom[resType]
                                    where partResourcesByDockedVessel[curVessel][resType].Contains(res)
                                    select res).ToList<PartResource>().ForEach(
                                        res => transfersFrom[resType].Remove(res));
                            }
                            GUI.color = color;
                        }
                        if (num2 == count)
                        {
                            if (!GUILayout.Toggle(true, "Out", GUI.skin.button, new GUILayoutOption[]
                                {
                                    GUILayout.Width(75f)
                                }))
                            {
                                (
                                    from res in transfersFrom[resType]
                                    where partResourcesByDockedVessel[curVessel][resType].Contains(res)
                                    select res).ToList<PartResource>().ForEach(
                                        res => transfersFrom[resType].Remove(res));
                            }
                        }
                        else
                        {
                            if (num2 > 0)
                            {
                                GUI.color = Color.grey;
                            }
                            if (GUILayout.Toggle(false, "Out", GUI.skin.button, new GUILayoutOption[]
                                {
                                    GUILayout.Width(75f)
                                }))
                            {
                                (
                                    from res in partResourcesByDockedVessel[curVessel][resType]
                                    where !transfersFrom[resType].Contains(res)
                                    select res).ToList<PartResource>().ForEach(
                                        res => transfersFrom[resType].Add(res));
                                (
                                    from res in transfersTo[resType]
                                    where partResourcesByDockedVessel[curVessel][resType].Contains(res)
                                    select res).ToList<PartResource>().ForEach(
                                        res => transfersTo[resType].Remove(res));
                            }
                            GUI.color = color;
                        }
                        GUILayout.EndHorizontal();
                        if (flag2)
                        {
                            activeResource = resType;
                            GUILayout.BeginVertical(GUI.skin.scrollView, new GUILayoutOption[0]);
                            foreach (PartResource current in partResourcesByDockedVessel[curVessel][resType])
                            {
                                GUILayout.BeginHorizontal(new GUILayoutOption[0]);
                                GUILayout.Label(
                                    string.Format("{0} @ {1} of {2}", current.resourceName,
                                                  current.amount.ToString("F"), current.maxAmount.ToString("F")),
                                    new GUILayoutOption[]
                                        {
                                            GUILayout.ExpandWidth(true)
                                        });
                                bool flag3 = false;
                                bool flag4 = transfersFrom[resType].Contains(current);
                                if (transfersTo[resType].Contains(current))
                                {
                                    flag3 = true;
                                }
                                if (GUILayout.Toggle(flag3, "In", GUI.skin.button, new GUILayoutOption[]
                                    {
                                        GUILayout.Width(75f)
                                    }) != flag3)
                                {
                                    if (!flag3)
                                    {
                                        transfersTo[resType].Add(current);
                                        if (transfersFrom[resType].Contains(current))
                                        {
                                            transfersFrom[resType].Remove(current);
                                        }
                                    }
                                    else
                                    {
                                        transfersTo[resType].Remove(current);
                                    }
                                }
                                if (GUILayout.Toggle(flag4, "Out", GUI.skin.button, new GUILayoutOption[]
                                    {
                                        GUILayout.Width(75f)
                                    }) != flag4)
                                {
                                    if (!flag4)
                                    {
                                        transfersFrom[resType].Add(current);
                                        if (transfersTo[resType].Contains(current))
                                        {
                                            transfersTo[resType].Remove(current);
                                        }
                                    }
                                    else
                                    {
                                        transfersFrom[resType].Remove(current);
                                    }
                                }
                                GUILayout.EndHorizontal();
                                if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                                {
                                    if (highlighted != current.part)
                                    {
                                        if (highlighted != null)
                                        {
                                            highlighted.SetHighlightDefault();
                                            highlighted.SetHighlight(false);
                                            highlighted = null;
                                        }
                                        highlighted = current.part;
                                        highlighted.SetHighlightColor(Color.blue);
                                        highlighted.SetHighlight(true);
                                    }
                                    flag = true;
                                }
                            }
                            GUILayout.EndVertical();
                        }
                        else
                        {
                            if (activeResource == resType)
                            {
                                activeResource = 0;
                            }
                        }
                    }
                }
                GUILayout.EndVertical();
            }
            else
            {
                if (curVessel == activeGuiVessel)
                {
                    activeGuiVessel = 0u;
                }
            }
        }
        GUILayout.EndScrollView();
        transferRunning = GUILayout.Toggle(transferRunning, (transferRunning ? "Stop" : "Start") + " Transfer",
                                           GUI.skin.button, new GUILayoutOption[]
                                               {
                                                   GUILayout.ExpandWidth(true)
                                               });
        GUI.DragWindow();
        if (flag || highlighted == null) return;
        highlighted.SetHighlightDefault();
        highlighted.SetHighlight(false);
        highlighted = null;
    }

    private void drawGUI()
    {
        GUI.skin = HighLogic.Skin;
        WindowPos = GUILayout.Window(1, WindowPos, WindowGUI, "Fuel Transfer", new GUILayoutOption[2]
            {
                GUILayout.MinWidth(100f),
                GUILayout.MinHeight(100f)
            });
    }

    public override void OnAwake()
    {
        if (transfersFrom == null)
            transfersFrom = new Dictionary<int, List<PartResource>>();
        if (transfersTo == null)
            transfersTo = new Dictionary<int, List<PartResource>>();
        partResourcesByDockedVessel = new Dictionary<uint, Dictionary<int, List<PartResource>>>();
        vesselNamesByRootID = new Dictionary<uint, string>();
        fuelPulled = new Dictionary<int, double>();
        awakeRun = true;
        base.OnAwake();
    }

    public void RegisterGui()
    {
        if (WindowPos.x == 0.0 && WindowPos.y == 0.0)
            WindowPos = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 200, 400f, 400f);
        RenderingManager.AddToPostDrawQueue(3, drawGUI);
        guiRegistered = true;
    }

    public void DeregisterGui()
    {
        RenderingManager.RemoveFromPostDrawQueue(3, drawGUI);
        if (highlighted != null)
        {
            highlighted.SetHighlightDefault();
            highlighted.SetHighlight(false);
            highlighted = null;
        }
        guiRegistered = false;
    }

    public override void OnSave(ConfigNode node)
    {
        ConfigNode configNode1 = node.AddNode("TransfersFrom");
        foreach (KeyValuePair<int, List<PartResource>> keyValuePair in transfersFrom)
        {
            ConfigNode configNode2 = configNode1.AddNode("Type");
            configNode2.AddValue("ID", keyValuePair.Key);
            foreach (PartResource partResource in keyValuePair.Value)
            {
                ConfigNode configNode3 = configNode2.AddNode("PartRes");
                configNode3.AddValue("ID", partResource.part.flightID);
                configNode3.AddValue("VesselID", partResource.part.vessel.id);
            }
        }
        ConfigNode configNode4 = node.AddNode("TransfersTo");
        foreach (KeyValuePair<int, List<PartResource>> keyValuePair in transfersTo)
        {
            ConfigNode configNode2 = configNode4.AddNode("Type");
            configNode2.AddValue("ID", keyValuePair.Key);
            foreach (PartResource partResource in keyValuePair.Value)
            {
                ConfigNode configNode3 = configNode2.AddNode("PartRes");
                configNode3.AddValue("ID", partResource.part.flightID);
                configNode3.AddValue("VesselID", partResource.part.vessel.id);
            }
        }
        base.OnSave(node);
    }

    public override void OnLoad(ConfigNode node)
    {
        if (transfersFromDelayed == null)
            transfersFromDelayed = new Dictionary<int, List<PartDef>>();
        if (transfersToDelayed == null)
            transfersToDelayed = new Dictionary<int, List<PartDef>>();
        if (node.HasNode("TransfersFrom"))
        {
            foreach (ConfigNode configNode1 in node.GetNode("TransfersFrom").GetNodes("Type"))
            {
                int key = int.Parse(configNode1.GetValue("ID"));
                if (!transfersFromDelayed.ContainsKey(key))
                    transfersFromDelayed.Add(key, new List<PartDef>());
                foreach (ConfigNode configNode2 in configNode1.GetNodes("PartRes"))
                    transfersFromDelayed[key].Add(new PartDef
                        {
                            VesselID = configNode2.GetValue("VesselID"),
                            PartID = uint.Parse(configNode2.GetValue("ID"))
                        });
            }
        }
        if (node.HasNode("TransfersTo"))
        {
            foreach (ConfigNode configNode1 in node.GetNode("TransfersTo").GetNodes("Type"))
            {
                int key = int.Parse(configNode1.GetValue("ID"));
                if (!transfersToDelayed.ContainsKey(key))
                    transfersToDelayed.Add(key, new List<PartDef>());
                foreach (ConfigNode configNode2 in configNode1.GetNodes("PartRes"))
                    transfersToDelayed[key].Add(new PartDef
                        {
                            VesselID = configNode2.GetValue("VesselID"),
                            PartID = uint.Parse(configNode2.GetValue("ID"))
                        });
            }
        }
        loadTime = DateTime.Now;
        base.OnLoad(node);
    }

    private struct PartDef
    {
        public string VesselID;
        public uint PartID;
    }
}