namespace HP.WindowsPrison.Restrictions
{
    using System;

    class CPU : Rule
    {
        public override RuleTypes RuleType
        {
            get
            {
                return RuleTypes.CPU;
            }
        }

        public override void Apply(Prison prison)
        {
            // prison.JobObject.CPUPercentageLimit = prison.Rules.CPUPercentageLimit;
        }

        public override void Destroy(Prison prison)
        {
        }

        public override RuleInstanceInfo[] List()
        {
            return new RuleInstanceInfo[0];
        }

        public override void Init()
        {
        }

        public override void Recover(Prison prison)
        {
        }
    }
}
