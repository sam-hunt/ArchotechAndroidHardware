namespace ArchotechAndroidHardware;

/// <summary>
/// Implemented by AAH reactor hediffs whose Energy backs VREA's
/// Need_ReactorPower. The NeedReactorPower_CurLevel patch enumerates known
/// reactor defs in order and pipes the first match's Energy into the need bar
/// (and vice versa for the setter). Add a hediff's DefOf field to
/// NeedReactorPowerPatchHelpers.ReactorDefs when introducing a new
/// implementer.
///
/// Energy is normalized [0, 1]; implementers clamp on set.
/// </summary>
public interface IAAHReactorEnergy
{
    float Energy { get; set; }
}
