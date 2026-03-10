using Robust.Shared.Serialization;

namespace Content.Shared.SecApartment;

[Serializable, NetSerializable]
public enum SquadIconNum : byte
{
    Alpha,
    Beta,
    Gamma,
    Delta,
    Epsilon,
    Zeta,
    Heta,
    Theta,
    Iota,
    Kappa,
    Lambda,
    Mu,
    Nu,
    Xi,
    Omicron,
    Pi,
    Ro,
    Sigma,
    Tau,
    Upsilon,
    Fi,
    Hi,
    Psi,
    Omega
}

[Serializable, NetSerializable]
public enum SquadStatus : byte
{
    Active,
    OnBreak
}
