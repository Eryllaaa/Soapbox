using UnityEngine;
using UnityEngine.UI;
using Soapbox.Builder.Vehicle;

namespace Soapbox.Builder.UI
{
    /// <summary>
    /// Displays live vehicle statistics by subscribing to a <see cref="VehicleStatsTracker"/>.
    /// </summary>
    public sealed class StatsPanelUI : MonoBehaviour
    {
        [SerializeField] private VehicleStatsTracker _tracker;
        [SerializeField] private Text _text;

        private void OnEnable()
        {
            if (_tracker == null) return;
            _tracker.StatsChanged += Refresh;
            Refresh(_tracker.Current);
        }

        private void OnDisable()
        {
            if (_tracker != null) _tracker.StatsChanged -= Refresh;
        }

        private void Refresh(VehicleStats s)
        {
            if (_text == null) return;

            _text.text =
                $"Parts: {s.PartCount}\n" +
                $"Weight: {s.TotalWeight:0.0} kg\n" +
                $"Cost: {s.TotalCost:0}\n" +
                $"Size (W/H/L): {s.Width:0.0} / {s.Height:0.0} / {s.Length:0.0}\n" +
                $"Wheels: {s.WheelCount}\n" +
                $"Seats: {s.SeatCount}\n" +
                $"CoM: ({s.CenterOfMass.x:0.00}, {s.CenterOfMass.y:0.00}, {s.CenterOfMass.z:0.00})";
        }
    }
}
