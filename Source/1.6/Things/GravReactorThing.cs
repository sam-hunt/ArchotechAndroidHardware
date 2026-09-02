using System.Text;
using Verse;

namespace ArchotechAndroidHardware;

// Item form of the grav reactor. Stores its current energy level so the
// reactor is transferable between android pawns: install → hediff inherits
// storedEnergy; eject → new item carries the hediff's current energy.
//
// Same shape as ThanaticReactorThing. The two classes are kept
// separate (rather than a shared base) so that GetInspectString and any
// future per-reactor cosmetic / behavioural details can diverge cleanly.
public class GravReactorThing : ThingWithComps
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
        string storedEnergyLine = "AAH_StoredEnergy".Translate((storedEnergy * 100f).ToString("F0"));
        sb.Append(storedEnergyLine);
        return sb.ToString().TrimEndNewlines();
    }
}
