using System.Text;
using Verse;

namespace ArchotechAndroidHardware;

// Item form of the Thanatic Reactor. Stores its current energy level so the
// reactor is transferable between android pawns: install → hediff inherits
// storedEnergy; eject → new item carries the hediff's current energy.
//
// Matches VREA's own VREAndroids.Reactor : ThingWithComps pattern
// (public float curEnergy field, ExposeData, GetInspectString showing the
// level). Unlike VREA's reactor, the install flow for this one actually
// wires the stored value onto the installed hediff — see
// ThanaticReactorInstallEnergyTransfer patch.
public class ThanaticReactorThing : ThingWithComps
{
    public float storedEnergy = 1f;

    public override void PostMake()
    {
        base.PostMake();
        storedEnergy = 1f;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref storedEnergy, "storedEnergy", 1f);
    }

    public override string GetInspectString()
    {
        var sb = new StringBuilder();
        var baseStr = base.GetInspectString();
        if (!baseStr.NullOrEmpty())
            sb.AppendLine(baseStr);
        sb.Append("Stored energy: ").Append((storedEnergy * 100f).ToString("F0")).Append('%');
        return sb.ToString().TrimEndNewlines();
    }
}
