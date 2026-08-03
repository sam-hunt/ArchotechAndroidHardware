using RimWorld;
using Verse;

namespace ArchotechAndroidHardware;

// Memory thought for transceiver-driven reprogramming whose mood penalty steps
// down as the memory ages — the android's unease at having been reprogrammed under
// outside (archotech) influence fades over time rather than vanishing at a cliff.
// Granted by VREAPatches.BehavioristStation_ConsumeSelfDetermination_Patch.
//
// A plain Thought_Memory stays at its forced stage (0) for its whole
// life; this override instead selects the stage from the memory's Thought_Memory.age:
//
//   age (days)   stage   mood
//   [0, 1)         0      -5
//   [1, 5)         1      -4
//   [5, 15)        2      -3
//   [15, 30)       3      -1
//
// The def's durationDays (30) governs final disappearance. The def's stage
// list must hold exactly the four entries above (indices 0-3), or CurStage
// would index out of range — keep the two in sync.
public class Thought_SelfDeterminationOverridden : Thought_Memory
{
    public override int CurStageIndex
    {
        get
        {
            float days = age / (float)GenDate.TicksPerDay;
            if (days < 1f) return 0;
            if (days < 5f) return 1;
            if (days < 15f) return 2;
            return 3;
        }
    }
}
