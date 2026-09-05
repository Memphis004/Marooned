using System;
using Marooned.Shared;
using MessagePipe;

namespace Marooned.Systems
{
    /// <summary>
    /// Ticks Hunger/Thirst/Mood/Fatigue over time and rolls for illness when a stat
    /// stays critical too long. Mirrors the reference project's TickGathering /
    /// TickCrafting fractional-accumulator pattern.
    /// </summary>
    public class SurvivalStatSystem
    {
        private readonly IPublisher<SurvivalStatChangedMessage> _statPublisher;
        private readonly IPublisher<ConditionCardAppliedMessage> _conditionPublisher;
        private readonly PlayerSurvivalState _state;

        private float _criticalHungerSeconds;
        private float _criticalThirstSeconds;

        public SurvivalStatSystem(
            IPublisher<SurvivalStatChangedMessage> statPublisher,
            IPublisher<ConditionCardAppliedMessage> conditionPublisher,
            GameStateProvider stateProvider)
        {
            _statPublisher = statPublisher;
            _conditionPublisher = conditionPublisher;
            _state = stateProvider.Player;
        }

        public void Tick(float deltaSeconds)
        {
            ApplyDrain("Hunger", ref _state.Hunger, 0.15f * deltaSeconds);
            ApplyDrain("Thirst", ref _state.Thirst, 0.25f * deltaSeconds);
            ApplyDrain("Fatigue", ref _state.Fatigue, -0.10f * deltaSeconds); // fatigue rises (negative "drain" = increase)

            if (_state.Hunger <= 15f)
            {
                _criticalHungerSeconds += deltaSeconds;
                if (_criticalHungerSeconds > 120f && !_state.ActiveConditionCardIds.Contains("illness_malnutrition"))
                    ApplyCondition("illness_malnutrition");
            }
            else _criticalHungerSeconds = 0f;

            if (_state.Thirst <= 15f)
            {
                _criticalThirstSeconds += deltaSeconds;
                if (_criticalThirstSeconds > 90f && !_state.ActiveConditionCardIds.Contains("illness_dehydration"))
                    ApplyCondition("illness_dehydration");
            }
            else _criticalThirstSeconds = 0f;

            if (_state.Hunger <= 0f && _state.Thirst <= 0f)
                _state.IsAlive = false;
        }

        private void ApplyDrain(string key, ref float value, float amount)
        {
            var before = value;
            value = Math.Clamp(value - amount, 0f, 100f);
            if (!Mathf_Approximately(before, value))
                _statPublisher.Publish(new SurvivalStatChangedMessage { StatKey = key, NewValue = value, Delta = value - before });
        }

        private static bool Mathf_Approximately(float a, float b) => Math.Abs(a - b) < 0.0001f;

        private void ApplyCondition(string conditionCardId)
        {
            _state.ActiveConditionCardIds.Add(conditionCardId);
            _conditionPublisher.Publish(new ConditionCardAppliedMessage { TargetEntityId = "player", ConditionCardId = conditionCardId });
        }
    }
}
