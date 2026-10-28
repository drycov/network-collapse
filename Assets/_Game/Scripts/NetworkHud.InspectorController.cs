using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NetworkCollapse
{
    public sealed partial class NetworkHud
    {
        public void SelectLink(int index)
        {
            SelectedId = -1;
            TargetId = -1;
            SelectedLinkId = index;
            Refresh();
        }

        public void SelectTarget(int id)
        {
            TargetId = id;
            Refresh();
        }

        private int FindSelectedLink() => model == null ? -1 : model.FindLink(SelectedId, TargetId);

        private int ActiveLink() => SelectedLinkId >= 0 ? SelectedLinkId : FindSelectedLink();

        public void Select(int id)
        {
            TargetId = -1;
            SelectedLinkId = -1;
            SelectedId = id;
            if (inspectorController != null && inspectorController.ValidNode(id))
            {
                var node = model.Nodes[id];
                vlanField.SetValueWithoutNotify(node.VlanId);
                mtuField.SetValueWithoutNotify(node.Mtu);
                desiredAdmin = node.AdminEnabled;
                desiredRouting = node.Routing;
                UpdateConfigButtons();
            }
            Refresh();
        }

        private void UpdateConfigButtons()
        {
            adminToggle.text = desiredAdmin ? "Административно: включено" : "Административно: выключено";
            routingToggle.text = "Routing: " + desiredRouting;
        }
    }
}
