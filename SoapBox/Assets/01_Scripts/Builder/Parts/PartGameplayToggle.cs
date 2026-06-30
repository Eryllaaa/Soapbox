using UnityEngine;

namespace Soapbox.Builder.Parts
{
    /// <summary>
    /// Enables or disables the driving-physics behaviours (<see cref="Wheel"/>,
    /// <see cref="Suspension"/>) on a part.
    ///
    /// While building there is no <see cref="Rigidbody"/> on the vehicle yet (the
    /// assembler adds it at "Test"), so these behaviours must stay disabled to avoid
    /// per-frame null-reference errors in their FixedUpdate. The assembler re-enables
    /// them once the Rigidbody exists.
    /// </summary>
    public static class PartGameplayToggle
    {
        /// <summary>
        /// Sets the enabled state of every <see cref="Wheel"/> and <see cref="Suspension"/>
        /// found under <paramref name="part"/> (inclusive).
        /// </summary>
        public static void SetGameplayEnabled(GameObject part, bool enabled)
        {
            if (part == null) return;

            Wheel[] wheels = part.GetComponentsInChildren<Wheel>(includeInactive: true);
            for (int i = 0; i < wheels.Length; i++)
                wheels[i].enabled = enabled;

            Suspension[] suspensions = part.GetComponentsInChildren<Suspension>(includeInactive: true);
            for (int i = 0; i < suspensions.Length; i++)
                suspensions[i].enabled = enabled;
        }
    }
}
