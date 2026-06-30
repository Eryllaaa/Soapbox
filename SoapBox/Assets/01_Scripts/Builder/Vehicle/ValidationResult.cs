using System.Collections.Generic;

namespace Soapbox.Builder.Vehicle
{
    /// <summary>Outcome of validating a vehicle, with a list of human-readable problems.</summary>
    public readonly struct ValidationResult
    {
        /// <summary>The problems found; empty when the vehicle is valid.</summary>
        public readonly IReadOnlyList<string> Errors;

        /// <summary>True when there are no problems.</summary>
        public bool IsValid => Errors == null || Errors.Count == 0;

        public ValidationResult(IReadOnlyList<string> errors) => Errors = errors;
    }
}
