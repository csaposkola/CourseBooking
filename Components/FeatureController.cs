using DotNetNuke.Entities.Modules;
using System.Collections.Generic;

namespace CourseBooking.Components
{
    public class FeatureController : IUpgradeable
    {
        public string UpgradeModule(string version)
        {
            return "Module upgraded successfully";
        }
    }
}