using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using System.Linq;

// Todo:
// add warning about selecting male / female
// try and sync hip movements
// Add recommended physics for hip thrust
// Fix not remembering female

public class SexHelper : MVRScript
{
    // Bodies
    private Atom _maleAtom;
    private FreeControllerV3 _penisBase;
    private FreeControllerV3 _penisMid;
    private FreeControllerV3 _penisTip;
    private FreeControllerV3 _malePelvisControl;
    private FreeControllerV3 _maleHipControl;
    private Rigidbody _malePelvis;
    private Rigidbody _gen1;
    private Rigidbody _gen2;
    private Rigidbody _gen3;

    private Atom _femaleAtom;
    private FreeControllerV3 _hipControl;
    private FreeControllerV3 _pelvisControl;
    private FreeControllerV3 _abdomenControl;
    private FreeControllerV3 _abdomen2Control;
    private Rigidbody _labiaTrigger;
    private Rigidbody _vaginaTrigger;
    private Rigidbody _deepVaginaTrigger;
    private Rigidbody _deeperVaginaTrigger;
    private Rigidbody _pelvis;

    // UI and settings
    private JSONStorableFloat _durationJSON;
    private JSONStorableFloat _durationRangeJSON;
    private JSONStorableFloat _durationUpdateIntervalJSON;

    private JSONStorableStringChooser _maleAtomJSON;
    private JSONStorableFloat _penisPositionOneJSON;
    private JSONStorableFloat _penisPositionTwoJSON;
    private JSONStorableFloat _penisRotationJSON;
    private JSONStorableFloat _penisPositionJSON;
    private JSONStorableBool _maleThrustEnabledJSON;
    private JSONStorableBool _maleAlignEnabledJSON;

    private JSONStorableStringChooser _femaleAtomJSON;
    private JSONStorableFloat _hipPositionOneJSON;
    private JSONStorableFloat _hipPositionTwoJSON;
    private JSONStorableFloat _hipRangeJSON;
    private JSONStorableBool _femaleThrustEnabledJSON;

    public JSONStorableStringChooser _easingJSON;
    public Func<float, float> easing;

    // Variables
    private bool _activeThrust = false;

    private float _lerpTime;
    private float _lerpTimer;
    private float _durationUpdateTimer;

    private Vector3 _penisTarget;
    private Vector3 _penisStart;
    private Vector3 _penisCurrent;
    private Vector3 _penisAngleOffset;
    private Vector3 _penisPositionOffset;
    private float _penisPosition;
    private bool _penisIsPositionOne = false;
    private Vector3 _penisResetPosition;

    private Vector3 _hipTarget;
    private Vector3 _hipStart;
    private Vector3 _hipCurrent;

    private float _perc; // percent of lerp complete

    private float _hipPosition;
    private bool _hipIsPositionOne = false;

    private Vector3 _hipResetPosition;
    private Quaternion _hipResetRotation;

    public override void Init()
    {
        try
        {
            SuperController.LogMessage($"{nameof(SexHelper)} initialized");

            // Easing Setup
            Easing.SetEasingChoices();
            _easingJSON = new JSONStorableStringChooser("Easing Choice", Easing.easingChoicesList, "Linear", "Select Motion Easing", SetEasing)
            {
                storeType = JSONStorableParam.StoreType.Full,
                val = "Linear"
            };
            SetEasing("Linear");
            RegisterStringChooser(_easingJSON);

            //Atom choices setup
            _maleAtomJSON = new JSONStorableStringChooser("MaleAtom", null, null, "Male", SyncMaleAtom)
            {
                storeType = JSONStorableStringChooser.StoreType.Full
            };

            // Try and load existing value
            if (_maleAtomJSON.val != null)
            {
                SyncMaleAtom(_maleAtomJSON.val);
            }
            RegisterStringChooser(_maleAtomJSON);

            _femaleAtomJSON = new JSONStorableStringChooser("FemaleAtom", null, null, "Female", SyncFemaleAtom)
            {
                storeType = JSONStorableStringChooser.StoreType.Full
            };

            if (_femaleAtomJSON.val != null)
            {
                SyncFemaleAtom(_femaleAtomJSON.val);
            }
            RegisterStringChooser(_femaleAtomJSON);

            // UI Setup
            var text = CreateTextField(new JSONStorableString("text", "<b>\nSelect Male</b>"), true);
            text.height = 50f;

            text = CreateTextField(new JSONStorableString("text", "<b>\n Select Female</b>"), true);
            text.height = 50f;

            // Male Atom selector popup
            RegisterStringChooser(_maleAtomJSON);
            SyncAtomChoices();
            UIDynamicPopup dp = CreateScrollablePopup(_maleAtomJSON, false);
            dp.popupPanelHeight = 1100f;
            dp.popup.onOpenPopupHandlers += SyncAtomChoices;

            // Female Atom selector popup
            RegisterStringChooser(_femaleAtomJSON);
            SyncAtomChoices();
            dp = CreateScrollablePopup(_femaleAtomJSON, false);
            dp.popupPanelHeight = 1100f;
            dp.popup.onOpenPopupHandlers += SyncAtomChoices;

            // Toggles
            text = CreateTextField(new JSONStorableString("text", "<b>\nToggle Features</b>"), true);
            text.height = 100f;
            var spacer = CreateSpacer(false);
            spacer.height = 35f;
            text = CreateTextField(new JSONStorableString("text", "\nOnly enable one type of thrust at a time"), true);
            text.height = 120;
            _maleAlignEnabledJSON = new JSONStorableBool("Enable Penis Alignment", false, StartMaleAlign);
            CreateToggle(_maleAlignEnabledJSON, false);
            RegisterBool(_maleAlignEnabledJSON);
            _maleAlignEnabledJSON.storeType = JSONStorableParam.StoreType.Full;

            _maleThrustEnabledJSON = new JSONStorableBool("Enable Penis Thrust", false, StartMaleThrust);
            CreateToggle(_maleThrustEnabledJSON, false);
            RegisterBool(_maleThrustEnabledJSON);
            _maleThrustEnabledJSON.storeType = JSONStorableParam.StoreType.Full;

            _femaleThrustEnabledJSON = new JSONStorableBool("Enable Hip Thrust", false, StartFemaleThrust);
            CreateToggle(_femaleThrustEnabledJSON, false);
            RegisterBool(_femaleThrustEnabledJSON);
            _femaleThrustEnabledJSON.storeType = JSONStorableParam.StoreType.Full;

            // Duration sliders
            text = CreateTextField(new JSONStorableString("text", "<b>\nTimings</b>"),true);
            text.height = 100f;
            spacer = CreateSpacer();
            spacer.height = 105f;
            _durationJSON = new JSONStorableFloat("Thrust Time", 0.20f, 0f, 1f, false);
            RegisterFloat(_durationJSON);
            _durationJSON.storeType = JSONStorableParam.StoreType.Full;
            CreateSlider(_durationJSON);
            text = CreateTextField(new JSONStorableString("text", "\nThe amount of time to complete a single thrust"), true);
            text.height = 120;

            _durationRangeJSON = new JSONStorableFloat("Thrust Time Range", 0.1f, 0f, 1f, false);
            RegisterFloat(_durationRangeJSON);
            _durationRangeJSON.storeType = JSONStorableParam.StoreType.Full;
            CreateSlider(_durationRangeJSON);

            text = CreateTextField(new JSONStorableString("text", "\nThe time range within which the thrust time can randomly vary"), true);
            text.height = 120;

            _durationUpdateIntervalJSON = new JSONStorableFloat("Thrust Time Interval", 5f, 0f, 50f, false);
            RegisterFloat(_durationUpdateIntervalJSON);
            _durationUpdateIntervalJSON.storeType = JSONStorableParam.StoreType.Full;
            CreateSlider(_durationUpdateIntervalJSON);

            text = CreateTextField(new JSONStorableString("text", "\nThe amount of time between the selection of each new thrust time"), true);
            text.height = 120;

            CreateScrollablePopup(_easingJSON).popupPanelHeight = 1100f;
            text = CreateTextField(new JSONStorableString("text", "\nThe type of easing applied to the thrust motion"), true);
            text.height = 100;

            text = CreateTextField(new JSONStorableString("text", "<b>\nPositioning</b>"), true);
            text.height = 100;
            spacer = CreateSpacer();
            spacer.height = 100f;

            _penisPositionOneJSON = new JSONStorableFloat("Penis In", 0f, -5f, 5f, false);
            RegisterFloat(_penisPositionOneJSON);
            _penisPositionOneJSON.storeType = JSONStorableParam.StoreType.Full;
            CreateSlider(_penisPositionOneJSON);
            text = CreateTextField(new JSONStorableString("text", "\nHigher = deeper\nBut be careful of physics explosions"), true);
            text.height = 120;

            _penisPositionTwoJSON = new JSONStorableFloat("Penis Out", -2.0f, -5f, 5f, false);
            RegisterFloat(_penisPositionTwoJSON);
            _penisPositionTwoJSON.storeType = JSONStorableParam.StoreType.Full;
            CreateSlider(_penisPositionTwoJSON);
            text = CreateTextField(new JSONStorableString("text", ""), true);
            text.height = 120;

            _hipPositionOneJSON = new JSONStorableFloat("Hip In", 0f, -3f, 3f, false);
            RegisterFloat(_hipPositionOneJSON);
            _hipPositionOneJSON.storeType = JSONStorableParam.StoreType.Full;
            CreateSlider(_hipPositionOneJSON);
            text = CreateTextField(new JSONStorableString("text", "\nOnly applies when Hip Thrust is enabled"), true);
            text.height = 120;

            _hipPositionTwoJSON = new JSONStorableFloat("Hip Out", 1f, -3f, 3f, false);
            RegisterFloat(_hipPositionTwoJSON);
            _hipPositionTwoJSON.storeType = JSONStorableParam.StoreType.Full;
            CreateSlider(_hipPositionTwoJSON);
            text = CreateTextField(new JSONStorableString("text", ""), true);
            text.height = 120;

            _penisPositionJSON = new JSONStorableFloat("Penis Position Up/Down", 0f, -1f, 1f, false);
            RegisterFloat(_penisPositionJSON);
            _penisPositionJSON.storeType = JSONStorableParam.StoreType.Full;
            CreateSlider(_penisPositionJSON);
            text = CreateTextField(new JSONStorableString("text", "Can be useful for positioning, but it's generally better to adjust female hip node instead"), true);
            text.height = 120;

            _penisRotationJSON = new JSONStorableFloat("Penis Angle Up/Down", 0f, -5f, 5f, false);
            RegisterFloat(_penisRotationJSON);
            _penisRotationJSON.storeType = JSONStorableParam.StoreType.Full;
            CreateSlider(_penisRotationJSON);
            text = CreateTextField(new JSONStorableString("text", ""), true);
            text.height = 120;

            text = CreateTextField(new JSONStorableString("text", "<b>\nTweaks</b>"),true);
            text.height = 100;
            spacer = CreateSpacer();
            spacer.height = 100f;

            var btn = CreateButton("Apply Recommended Physics Settings");
            btn.button.onClick.AddListener(() => { ApplyPhysics(); });
            btn.buttonColor = Color.blue;
            btn.textColor = Color.white;

            spacer = CreateSpacer();
            spacer.height = 180f;

            text = CreateTextField(new JSONStorableString("text", "\nIncreases hold position and rotation strength on penis and female hip\n\nReduces hold position strength on male hip and pelvis"), true);
            text.height = 240;

            btn = CreateButton("Apply Recommended Control Settings");
            btn.button.onClick.AddListener(() => { ApplyControls(); });
            btn.buttonColor = Color.blue;
            btn.textColor = Color.white;

            text = CreateTextField(new JSONStorableString("text", "\nTurns off male pelvis\nTurns on female hip\nTurns off female pelvis, abdomen and abdomen 2"), true);
            text.height = 240;

            text = CreateTextField(new JSONStorableString("text", "<b>\nTips</b>"), true);
            text.height = 100;

            text = CreateTextField(new JSONStorableString("text", "\nThe best way to improve motion is usually to edit physics settings for both characters. The key values to tweak are:\nHold Position Spring\nHold Position Max Force\nHold Rotation Spring\nHold Rotation Max Force. \n\nKey nodes to tweak are:\nAny active nodes\nPenis Base\nHips\nPelvis\nAbdomen and Abdomen2\nThighs\nFeet\n\nIf you are getting physics explosions:\nReduce 'Penis In' value\nReduce Hold Position Strength on Penis Base and Female Hip\n\nIt will still explode eventually :("),true);
            text.height = 1000;
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(SexHelper)}.{nameof(Init)}: {e}");
        }
    }

    public void OnEnable()
    {
        try
        {
            SuperController.LogMessage($"{nameof(SexHelper)} enabled");
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(SexHelper)}.{nameof(Init)}: {e}");
        }
    }

    // FixedUpdate is called with each physics simulation frame by Unity
    void FixedUpdate()
    {
        try
        {
            if (_maleAtom != null && _femaleAtom != null)
            {
                if (_maleThrustEnabledJSON.val || _femaleThrustEnabledJSON.val)
                {
                    // Update Timers;
                    _durationUpdateTimer += Time.fixedDeltaTime;
                    _lerpTimer += Time.fixedDeltaTime;

                    if (_lerpTimer > _lerpTime)
                    {
                        _lerpTimer = _lerpTime;
                    }

                    // Update Targets
                    _penisTarget = (_vaginaTrigger.transform.position + (_deeperVaginaTrigger.transform.position - _vaginaTrigger.transform.position) * _penisPosition);
                    _hipTarget = (_penisBase.transform.position + (_penisBase.transform.forward) * (_hipPosition / 10)); // divide by 10 to make the hip sliders more reasonable
                    // Add penis target offset
                    _penisPositionOffset.x = 0;
                    _penisPositionOffset.y = _penisPositionJSON.val;
                    _penisPositionOffset.z = 0;
                    _penisTarget += _penisPositionOffset;

                }

                // update penis position
                if (_maleThrustEnabledJSON.val)
                {
                    _perc = _lerpTimer / _lerpTime;
                    // add easing
                    _perc = easing(_perc);
                    _penisCurrent = Vector3.Lerp(_penisStart, _penisTarget, _perc);
                    _penisBase.transform.position = _penisCurrent;
                }

                if (_penisCurrent == _penisTarget && _maleThrustEnabledJSON.val)
                {
                    NewThrust("penis");
                }

                // update hip position
                if (_femaleThrustEnabledJSON.val)
                {
                    _perc = _lerpTimer / _lerpTime;
                    _perc = easing(_perc);
                    _hipCurrent = Vector3.Lerp(_hipStart, _hipTarget, _perc);
                    _hipControl.transform.position = _hipCurrent;
                }

                if (_hipCurrent == _hipTarget && _femaleThrustEnabledJSON.val)
                {
                    NewThrust("hip");
                }

                if (_maleAlignEnabledJSON.val)
                {
                    UpdateRotations();
                }
            }
        }
        catch (Exception e)
        {
            SuperController.LogError("Exception caught: " + e);
        }
    }

    private void NewThrust(string type)
    {
        if (type == "penis")
        {
            SetNewPenisTarget();
        }

        if (type == "hip")
        {
            SetNewHipTarget();
        }
    }

    private void SetNewPenisTarget()
    {
        _penisStart = _penisTarget;
        _lerpTimer = 0f;
        _lerpTime = SetNewDurationTimer();
        if (_penisIsPositionOne)
        {
            _penisPosition = _penisPositionTwoJSON.val;
            _penisIsPositionOne = false;
        }
        else
        {
            _penisPosition = _penisPositionOneJSON.val;
            _penisIsPositionOne = true;
        }

    }
    private void SetNewHipTarget()
    {
        _hipStart = _hipTarget;
        _lerpTimer = 0f;
        _lerpTime = SetNewDurationTimer();
        if (_hipIsPositionOne)
        {
            _hipPosition = _hipPositionTwoJSON.val;
            _hipIsPositionOne = false;

        }
        else
        {
            _hipPosition = _hipPositionOneJSON.val;
            _hipIsPositionOne = true;
        }
    }

    private float SetNewDurationTimer()
    {
        if (_durationRangeJSON.val == 0 || _durationUpdateTimer > _durationUpdateIntervalJSON.val)
        {
            _durationUpdateTimer = 0;
            float min = _durationJSON.val - (_durationRangeJSON.val);
            if (min < 0)
            {
                min = 0.1f;
            }
            float max = _durationJSON.val + (_durationRangeJSON.val);
            return UnityEngine.Random.Range(min, max);
        }
        else
        {
            return _lerpTime; // If it's not time to set a new duration, return the current duration
        }
    }

    private void StartMaleAlign(bool isChecked)
    {
        if (isChecked)
        {
            _penisBase.currentRotationState = FreeControllerV3.RotationState.On;
            _penisMid.currentRotationState = FreeControllerV3.RotationState.On;
            _penisTip.currentRotationState = FreeControllerV3.RotationState.On;
        }
        else
        {
            _penisBase.currentRotationState = FreeControllerV3.RotationState.Off;
            _penisMid.currentRotationState = FreeControllerV3.RotationState.Off;
            _penisTip.currentRotationState = FreeControllerV3.RotationState.Off;
        }
    }
    private void StartMaleThrust(bool isChecked)
    {
        if (isChecked)
        {
            _lerpTime = _durationJSON.val;
            _penisStart = _penisTip.transform.position;
            _penisBase.currentPositionState = FreeControllerV3.PositionState.On;
            _penisMid.currentPositionState = FreeControllerV3.PositionState.Off;
            _penisTip.currentPositionState = FreeControllerV3.PositionState.Off;
            _penisBase.RBHoldPositionSpring = 10000;
        }
        else
        {
            _penisBase.currentPositionState = FreeControllerV3.PositionState.Off;
        }
    }

    private void StartFemaleThrust(bool isChecked)
    {
        if (isChecked)
        {
            _lerpTime = _durationJSON.val;
            _hipStart = _hipControl.transform.position;
        }
    }

    private void ApplyPhysics()
    {
        _penisBase.RBHoldPositionSpring = 10000;
        _maleHipControl.RBHoldPositionSpring = 200;
        _malePelvisControl.RBHoldPositionSpring = 200;
        _hipControl.RBHoldPositionSpring = 8000;
        _hipControl.RBHoldRotationSpring = 10000;
    }

    private void ApplyControls()
    {
        _hipControl.currentPositionState = FreeControllerV3.PositionState.On;
        _hipControl.currentRotationState = FreeControllerV3.RotationState.On;
        _pelvisControl.currentPositionState = FreeControllerV3.PositionState.Off;
        _pelvisControl.currentRotationState = FreeControllerV3.RotationState.Off;
        _abdomenControl.currentPositionState = FreeControllerV3.PositionState.Off;
        _abdomenControl.currentRotationState = FreeControllerV3.RotationState.Off;
        _abdomen2Control.currentPositionState = FreeControllerV3.PositionState.Off;
        _abdomen2Control.currentRotationState = FreeControllerV3.RotationState.Off;
        _malePelvisControl.currentPositionState = FreeControllerV3.PositionState.Off;
        _malePelvisControl.currentRotationState = FreeControllerV3.RotationState.Off;
    }

    private void UpdateRotations()
    {
        _penisAngleOffset.y = _penisRotationJSON.val;

        _gen1.transform.LookAt(_deeperVaginaTrigger.transform.position + _penisAngleOffset);
        _gen2.transform.LookAt(_deeperVaginaTrigger.transform.position + _penisAngleOffset);
        _gen3.transform.LookAt(_deeperVaginaTrigger.transform.position + _penisAngleOffset);

    }

    private void UpdateHipRotation(float val)
    {
        _hipControl.transform.rotation = Quaternion.AngleAxis(val, _hipControl.transform.right);
    }

    public void SetEasing(string aEasing)
    {
        foreach (var pair in Easing.easingChoices)
        {
            if (pair.Key == aEasing)
            {
                easing = pair.Value;
                break;
            }
        }
    }

    protected void SyncFemaleAtom(string atomUID)
    {
        _femaleAtom = SuperController.singleton.GetAtomByUid(atomUID);
        _hipControl = _femaleAtom.freeControllers.First(fc => fc.name == "hipControl");
        _pelvisControl = _femaleAtom.freeControllers.First(fc => fc.name == "pelvisControl");
        _abdomenControl = _femaleAtom.freeControllers.First(fc => fc.name == "abdomenControl");
        _abdomen2Control = _femaleAtom.freeControllers.First(fc => fc.name == "abdomen2Control");
        _labiaTrigger = _femaleAtom.rigidbodies.First(rb => rb.name == "LabiaTrigger");
        _vaginaTrigger = _femaleAtom.rigidbodies.First(rb => rb.name == "VaginaTrigger");
        _deepVaginaTrigger = _femaleAtom.rigidbodies.First(rb => rb.name == "DeepVaginaTrigger");
        _deeperVaginaTrigger = _femaleAtom.rigidbodies.First(rb => rb.name == "DeeperVaginaTrigger");
        _pelvis = _femaleAtom.rigidbodies.First(rb => rb.name == "pelvis");
    }

    protected void SyncMaleAtom(string atomUID)
    {
        _maleAtom = SuperController.singleton.GetAtomByUid(atomUID);

        try
        {
            _penisBase = _maleAtom.freeControllers.First(fc => fc.name == "penisBaseControl");
            _penisMid = _maleAtom.freeControllers.First(fc => fc.name == "penisMidControl");
            _penisTip = _maleAtom.freeControllers.First(fc => fc.name == "penisTipControl");
            _malePelvisControl = _maleAtom.freeControllers.First(fc => fc.name == "pelvisControl");
            _malePelvis = _maleAtom.rigidbodies.First(rb => rb.name == "pelvis");
            _maleHipControl = _maleAtom.freeControllers.First(fc => fc.name == "hipControl");
            _gen1 = _maleAtom.rigidbodies.First(rb => rb.name == "Gen1");
            _gen2 = _maleAtom.rigidbodies.First(rb => rb.name == "Gen2");
            _gen3 = _maleAtom.rigidbodies.First(rb => rb.name == "Gen3");
        }
        catch
        {
            SuperController.LogMessage("This character doesn't have a penis");
        }
    }

    protected void SyncAtomChoices()
    {
        List<string> atomChoices = new List<string>
        {
            "None"
        };
        _femaleAtomJSON.choices = SuperController.singleton.GetAtoms().Where(atom => atom.GetStorableByID("geometry") != null).Select(atom => atom.name).ToList();
        _maleAtomJSON.choices = SuperController.singleton.GetAtoms().Where(atom => atom.GetStorableByID("geometry") != null).Select(atom => atom.name).ToList();
    }

    public void OnDisable()
    {
        try
        {
            SuperController.LogMessage($"{nameof(SexHelper)} disabled");
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(SexHelper)}.{nameof(Init)}: {e}");
        }
    }

    public void OnDestroy()
    {
        try
        {
            SuperController.LogMessage($"{nameof(SexHelper)} destroyed");
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(SexHelper)}.{nameof(Init)}: {e}");
        }
    }
}

#region Easings code

public class Easing
{

    public static Dictionary<string, Func<float, float>> easingChoices;
    public static List<string> easingChoicesList;

    public static void SetEasingChoices()
    {
        easingChoices = new Dictionary<string, Func<float, float>>()
        {
            {"Linear", f => Easing.Linear(f)},
            {"Quadratic In", f => Easing.Quadratic.In(f)},
            {"Quadratic Out", f => Easing.Quadratic.Out(f)},
            {"Quadratic InOut", f => Easing.Quadratic.InOut(f)},
            {"Cubic In", f => Easing.Cubic.In(f)},
            {"Cubic Out", f => Easing.Cubic.Out(f)},
            {"Cubic InOut", f => Easing.Cubic.InOut(f)},
            {"Quartic In", f => Easing.Quartic.In(f)},
            {"Quartic Out", f => Easing.Quartic.Out(f)},
            {"Quartic InOut", f => Easing.Quartic.InOut(f)},
            {"Quintic In", f => Easing.Quintic.In(f)},
            {"Quintic Out", f => Easing.Quintic.Out(f)},
            {"Quintic InOut", f => Easing.Quintic.InOut(f)},
            {"Sinusoidal In", f => Easing.Sinusoidal.In(f)},
            {"Sinusoidal Out", f => Easing.Sinusoidal.Out(f)},
            {"Sinusoidal InOut", f => Easing.Sinusoidal.InOut(f)},
            {"Exponential In", f => Easing.Exponential.In(f)},
            {"Exponential Out", f => Easing.Exponential.Out(f)},
            {"Exponential InOut", f => Easing.Exponential.InOut(f)},
            {"Circular In", f => Easing.Circular.In(f)},
            {"Circular Out", f => Easing.Circular.Out(f)},
            {"Circular InOut", f => Easing.Circular.InOut(f)},
            {"Elastic In", f => Easing.Elastic.In(f)},
            {"Elastic Out", f => Easing.Elastic.Out(f)},
            {"Elastic InOut", f => Easing.Elastic.InOut(f)},
            {"Back In", f => Easing.Back.In(f)},
            {"Back Out", f => Easing.Back.Out(f)},
            {"Back InOut", f => Easing.Back.InOut(f)},
            {"Bounce In", f => Easing.Bounce.In(f)},
            {"Bounce Out", f => Easing.Bounce.Out(f)},
            {"Bounce InOut", f => Easing.Bounce.InOut(f)},
        };

        easingChoicesList = new List<string>();
        foreach (var easingChoice in easingChoices)
        {
            easingChoicesList.Add(easingChoice.Key);
        }
    }

    public static float Linear(float k)
    {
        return k;
    }

    public class Quadratic
    {
        public static float In(float k)
        {
            return k * k;
        }

        public static float Out(float k)
        {
            return k * (2f - k);
        }

        public static float InOut(float k)
        {
            if ((k *= 2f) < 1f) return 0.5f * k * k;
            return -0.5f * ((k -= 1f) * (k - 2f) - 1f);
        }
    };

    public class Cubic
    {
        public static float In(float k)
        {
            return k * k * k;
        }

        public static float Out(float k)
        {
            return 1f + ((k -= 1f) * k * k);
        }

        public static float InOut(float k)
        {
            if ((k *= 2f) < 1f) return 0.5f * k * k * k;
            return 0.5f * ((k -= 2f) * k * k + 2f);
        }
    };

    public class Quartic
    {
        public static float In(float k)
        {
            return k * k * k * k;
        }

        public static float Out(float k)
        {
            return 1f - ((k -= 1f) * k * k * k);
        }

        public static float InOut(float k)
        {
            if ((k *= 2f) < 1f) return 0.5f * k * k * k * k;
            return -0.5f * ((k -= 2f) * k * k * k - 2f);
        }
    };

    public class Quintic
    {
        public static float In(float k)
        {
            return k * k * k * k * k;
        }

        public static float Out(float k)
        {
            return 1f + ((k -= 1f) * k * k * k * k);
        }

        public static float InOut(float k)
        {
            if ((k *= 2f) < 1f) return 0.5f * k * k * k * k * k;
            return 0.5f * ((k -= 2f) * k * k * k * k + 2f);
        }
    };

    public class Sinusoidal
    {
        public static float In(float k)
        {
            return 1f - Mathf.Cos(k * Mathf.PI / 2f);
        }

        public static float Out(float k)
        {
            return Mathf.Sin(k * Mathf.PI / 2f);
        }

        public static float InOut(float k)
        {
            return 0.5f * (1f - Mathf.Cos(Mathf.PI * k));
        }
    };

    public class Exponential
    {
        public static float In(float k)
        {
            return k == 0f ? 0f : Mathf.Pow(1024f, k - 1f);
        }

        public static float Out(float k)
        {
            return k == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * k);
        }

        public static float InOut(float k)
        {
            if (k == 0f) return 0f;
            if (k == 1f) return 1f;
            if ((k *= 2f) < 1f) return 0.5f * Mathf.Pow(1024f, k - 1f);
            return 0.5f * (-Mathf.Pow(2f, -10f * (k - 1f)) + 2f);
        }
    };

    public class Circular
    {
        public static float In(float k)
        {
            return 1f - Mathf.Sqrt(1f - k * k);
        }

        public static float Out(float k)
        {
            return Mathf.Sqrt(1f - ((k -= 1f) * k));
        }

        public static float InOut(float k)
        {
            if ((k *= 2f) < 1f) return -0.5f * (Mathf.Sqrt(1f - k * k) - 1);
            return 0.5f * (Mathf.Sqrt(1f - (k -= 2f) * k) + 1f);
        }
    };

    public class Elastic
    {
        public static float In(float k)
        {
            if (k == 0) return 0;
            if (k == 1) return 1;
            return -Mathf.Pow(2f, 10f * (k -= 1f)) * Mathf.Sin((k - 0.1f) * (2f * Mathf.PI) / 0.4f);
        }

        public static float Out(float k)
        {
            if (k == 0) return 0;
            if (k == 1) return 1;
            return Mathf.Pow(2f, -10f * k) * Mathf.Sin((k - 0.1f) * (2f * Mathf.PI) / 0.4f) + 1f;
        }

        public static float InOut(float k)
        {
            if ((k *= 2f) < 1f) return -0.5f * Mathf.Pow(2f, 10f * (k -= 1f)) * Mathf.Sin((k - 0.1f) * (2f * Mathf.PI) / 0.4f);
            return Mathf.Pow(2f, -10f * (k -= 1f)) * Mathf.Sin((k - 0.1f) * (2f * Mathf.PI) / 0.4f) * 0.5f + 1f;
        }
    };

    public class Back
    {
        static readonly float _s = 1.70158f;
        static readonly float _s2 = 2.5949095f;

        public static float In(float k)
        {
            return k * k * ((_s + 1f) * k - _s);
        }

        public static float Out(float k)
        {
            return (k -= 1f) * k * ((_s + 1f) * k + _s) + 1f;
        }

        public static float InOut(float k)
        {
            if ((k *= 2f) < 1f) return 0.5f * (k * k * ((_s2 + 1f) * k - _s2));
            return 0.5f * ((k -= 2f) * k * ((_s2 + 1f) * k + _s2) + 2f);
        }
    };

    public class Bounce
    {
        public static float In(float k)
        {
            return 1f - Out(1f - k);
        }

        public static float Out(float k)
        {
            if (k < (1f / 2.75f))
            {
                return 7.5625f * k * k;
            }
            else if (k < (2f / 2.75f))
            {
                return 7.5625f * (k -= (1.5f / 2.75f)) * k + 0.75f;
            }
            else if (k < (2.5f / 2.75f))
            {
                return 7.5625f * (k -= (2.25f / 2.75f)) * k + 0.9375f;
            }
            else
            {
                return 7.5625f * (k -= (2.625f / 2.75f)) * k + 0.984375f;
            }
        }

        public static float InOut(float k)
        {
            if (k < 0.5f) return In(k * 2f) * 0.5f;
            return Out(k * 2f - 1f) * 0.5f + 0.5f;
        }
    };
}

#endregion

