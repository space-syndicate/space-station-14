using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Content.Server.Speech.Components;

/// <summary>
///     Lizard specific properties
/// </summary>
[RegisterComponent]
public sealed class LizardBloodComponent : Component
{
    public EntityUid Uid;



    /// <summary>
    /// The temperature at which the lizard sleeps
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("Sleepy temperature")]
    private float _sleepyTemperature = 240.0f;
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("Comfortable temperature")]
    private float _comfortableTemperature = 340f;
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("Current temperature")]
    private float _currentTemperature;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("isSleep")]
    private bool _isSleeping;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("Blood in part")]
    private float _bloodInPart = 0.5f;

    public float BloodInPart
    {
        get { return _bloodInPart; }
    }

    [ViewVariables(VVAccess.ReadOnly)]
    [DataField("ReagentProduce")]
    private float _reagentProduseModify = 1;

    /// <summary>
    /// DEBUG blood tick delay
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("DEBUG blood tick delay")]
    private int _bloodTickDelay = 100;
    private int _currentTickDelay = 0;
    private bool _isTickDelay;
    public int BloodTickDelay
    {
        get { return _bloodTickDelay; }

    }

    public int CurrentTickDelay
    {
        get { return _currentTickDelay; }
        set { _currentTickDelay = value; }
    }
    public void ResetTickDelay()
    {
        _currentTickDelay = 0;
    }

    public void LizardIsComfortable()
    {
        _reagentProduseModify = 2;
    }
    public void LizardIsFine()
    {
        _reagentProduseModify = 1;
    }
    public bool IsTickDelay
    {
        get { return _isTickDelay; }
        set { _isTickDelay = value; }
    }


    /// <summary>
    /// Zessul's blood reagent count
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("Zessul's Blood count")]
    private float _zessulBloodCount = 0f;

    /// <summary>
    /// Maximum count Zessul's blood
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("Zessul's Blood maximum count")]
    private float _maxZessulBloodCount = 3.0f;

    public bool IsSleep
    {
        get { return _isSleeping; }
        set { _isSleeping = value; }
    }

    public float CurrentTemperature
    {
        get { return _currentTemperature; }
        set { _currentTemperature = value; }
    }

    public float SleepyTemperature
    {
        get { return _sleepyTemperature; }
    }
    public float ComfortableTemperature
    {
        get { return _comfortableTemperature; }
    }

    public float ZessulBloodCount
    {
        get { return _zessulBloodCount; }
    }
    public void ProduceReagent()
    {
        _zessulBloodCount += _bloodInPart * _reagentProduseModify;
    }
    public float MaxZessulBloodCount
    {
        get { return _maxZessulBloodCount; }
    }

    internal void ColdCheck(OnTemperatureChangeEvent temperature)
    {
        _currentTemperature = temperature.CurrentTemperature;

        if (_currentTemperature <= _sleepyTemperature)
        {
            _isSleeping = true;
        }
        else _isSleeping = false;
    }
}
