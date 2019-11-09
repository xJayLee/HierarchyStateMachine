﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HSMTree;
using GenPB;

public class AbilityHSM : IAbilityEnvironment, IAction
{
    private Skill _skill;

    private HSMStateMachine _hsmStateMachine = null;
    private IConditionCheck _iconditionCheck = null;
    private AbilityInputExtend _abilityInputExtend = null;

    protected const string SkillRequestState = "Skill_Change_State";
    protected const string EnableFire        = "EnableFire";
    protected const string EnergyEnougth     = "EnergyEnougth";
    protected const string EnableActive      = "EnableActive";
    protected const string PhaseOrHold       = "PhaseOrHold";
    protected const string FocoFull          = "FocoFull";
    protected const string ShotTimeEnd       = "ShotTimeEnd";

    protected Dictionary<int, string> _btnNameDic = new Dictionary<int, string>() {
        {(int)AbilityButtonType.GENERAL,      "GenericBtn" }, // 普通技能按钮
        {(int)AbilityButtonType.SKILL_DEPUTY, "DeputyBtn" },  // 副技能按钮
        {(int)AbilityButtonType.SKILL_UNIQUE, "UniqueBtn" },  // 大招按钮
        {(int)AbilityButtonType.FIRE,         "FireBtn" },    // 释放按钮
        {(int)AbilityButtonType.TRANSFER,     "TransferBtn" },// 传送按钮
    };

    
    protected Dictionary<int, string> _skillConfigDic = new Dictionary<int, string>() {
        { (int)SkillHandleType.CONTINUOUS_FIRE,        "AbilityGenericHSM"},     // 普攻-连射     //已测试
        { (int)SkillHandleType.ROLL_BRUSH,             ""},                      // 普攻-滚动
        { (int)SkillHandleType.FOCO_SINGLE_FIRE,       "AbilityFocoSingleHSM"},  // 普攻-蓄力单发
        { (int)SkillHandleType.FOCO_CONTINUOUS_FIRE,   "AbilityFocoContinueHSM"},// 普攻-蓄力连射
        { (int)SkillHandleType.SWING_BRUSH,            ""},                      // 刷子-甩
        { (int)SkillHandleType.SINGLE_SHOT,            ""},                      // 普攻-单发
        { (int)SkillHandleType.DEPUTY_SKILL_THROW,     "AbilityThrowHSM"},       // 副技能-投掷   //已测试
        { (int)SkillHandleType.DEPUTY_SKILL_FOCO,      "AbilityFocoFullHSM"},    // 副技能-蓄力   //已测试
        { (int)SkillHandleType.UNIQUE_INSTANT_SKILL,   "AbilityUniqueHSM"},      // 大招-瞬发     //已测试
        { (int)SkillHandleType.UNIQUE_FIRE_CONTINUOUS, "AbilityUniqueRayHSM"},   // 大招-激光
        { (int)SkillHandleType.UNIQUE_FIRE_SKILL,      "" },                     // 大招-射击
        { (int)SkillHandleType.UNIQUE_DEPUTY_SKILL,    "" },                     // 大招-副技能
        { (int)SkillHandleType.TRANSFER,               "" },                     // 传送技能
    };

    public AbilityHSM(Skill skill)
    {
        _skill = skill;
        Init();
    }

    public void Init()
    {
        string configName = string.Empty;
        if (!_skillConfigDic.TryGetValue(_skill.SkillData.HandleType, out configName))
        {
            return;
        }

        TextAsset textAsset = Resources.Load<TextAsset>(configName);
        if (null == textAsset)
        {
            return;
        }

        Debug.LogError(configName);

        _iconditionCheck = new ConditionCheck();
        _abilityInputExtend = new AbilityInputExtend();

        HSMAnalysis analysis = new HSMAnalysis();
        _hsmStateMachine = analysis.Analysis(textAsset.text, _iconditionCheck, this);
        _hsmStateMachine.SetAutoTransitionState(false);

        Clear();
    }

    public void Update()
    {
        UpdateEnvironment();

        if (null != _hsmStateMachine)
        {
            _hsmStateMachine.Execute();
        }
    }

    #region ConditionCheck
    public ConditionCheck ConditionCheck
    {
        get { return (ConditionCheck)_iconditionCheck; }
    }

    public void Input(AbilityButtonType buttonType, AbilityHandleType handleType)
    {
        if (handleType == AbilityHandleType.PRESS)  // 只处理 Down、Up
        {
            return;
        }

        string btnName = string.Empty;
        if (!_btnNameDic.TryGetValue((int)buttonType, out btnName))
        {
            return;
        }

        Debug.LogError("Input:" + btnName + "   " + (int)handleType);
        ConditionCheck.SetParameter(btnName, (int)handleType);
    }

    public void ChangeState(SkillConfigSkillPhaseType phaseType)
    {
        bool value = (phaseType == SkillConfigSkillPhaseType.STANDBY_PHASE) ? true : false;
        ConditionCheck.SetParameter(PhaseOrHold, value);

        StateSkill stateSkill = GetState(phaseType);
        if (null != stateSkill)
        {
            _hsmStateMachine.ChangeState(stateSkill.StateId);
        }
    }

    private StateSkill GetState(SkillConfigSkillPhaseType phaseType)
    {
        StateSkill state = null;
        for (int i = 0; i < _hsmStateMachine.StateList.Count; ++i)
        {
            StateSkill temp = (StateSkill)_hsmStateMachine.StateList[i];
            if (temp.PhaseType == phaseType)
            {
                state = temp;
                break;
            }
        }

        return state;
    }
    #endregion

    public void SkillEnd()
    {
        Clear();
    }

    private void Clear()
    {
        ConditionCheck.SetParameter(PhaseOrHold, false);
        ConditionCheck.SetParameter(FocoFull, false);
    }

    public void Receive(int weaponId, SkillPhaseType type, float focoPercentage, int ret)
    {
        _abilityInputExtend.RemoveRequest(type);
    }

    public void Request(int roleId, int weaponId, int type, float focoPercentage)
    {
        if (_abilityInputExtend.IsWaitRequest((SkillPhaseType)type))
        {
            return;
        }
        
        Debug.LogError("Request: roleId:" + roleId + "    weaponId:" + weaponId + "    type:" + (SkillPhaseType)type);
        _abilityInputExtend.AddRequest((SkillPhaseType)type);
        SkillEventHandler.Instance.SendSkillOper(roleId, _skill.weaponId, (int)type, focoPercentage, 0);
    }

    public void SkillEvent(SkillConfigSkillPhaseType phaseType, SkillConfigSkillCustomEvent customEvent)
    {
        if (customEvent.EventBase.EventType != (int)SkillEventType.HOLD)
        {
            return;
        }
        //Debug.LogError("Hold:" + phaseType + "    " + (SkillEventType)(customEvent.EventBase.EventType));
        ConditionCheck.SetParameter(PhaseOrHold, true);
    }

    public void PhaseEnd(SkillConfigSkillPhaseType phaseType)
    {
        ConditionCheck.SetParameter(PhaseOrHold, true);
    }

    #region IAction
    public void DoAction(int toStateId)
    {
        StateSkill state = (StateSkill)(_hsmStateMachine.GetState(toStateId));
        if (null == state)
        {
            return;
        }

        SkillConfigSkillPhaseType type = state.PhaseType;
        SkillStateBase skillStateBase = _skill.skillStateMachine.GetState(type);
        skillStateBase.WillToThisState();
    }
    #endregion

    #region IAbilityEnvironment
    public void UpdateEnvironment()
    {
        if (null == ConditionCheck)
        {
            return;
        }
        int result = 0;

        ConditionCheck.SetParameter(EnableFire, _skill.EnableFire(ref result));
        ConditionCheck.SetParameter(EnergyEnougth, _skill.SkillEnergyEnough());
        ConditionCheck.SetParameter(EnableActive, _skill.EnableActive(ref result));
        ConditionCheck.SetParameter(ShotTimeEnd, _skill.ShotEndTime());
        //Debug.LogError(_skill.weaponId + "    energyEnougth:" + _skill.SkillEnergyEnough() + "    enableFire:" + _skill.EnableFire(ref result));

        FocoEnergia();
    }

    private void FocoEnergia()
    {
        if (null == _skill || null == _skill.FocoEnergia || !_skill.FocoEnergia.IsStart())
        {
            return;
        }
        bool focoFull = (_skill.FocoEnergia.GetPercentage() >= 1);
        ConditionCheck.SetParameter(FocoFull, focoFull);
    }
    #endregion

}
