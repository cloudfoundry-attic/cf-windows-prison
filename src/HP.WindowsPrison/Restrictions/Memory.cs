namespace HP.WindowsPrison.Restrictions
{
    class Memory : Rule
    {
        public override RuleTypes RuleType
        {
            get
            {
                return RuleTypes.Memory;
            }
        }

        public override void Apply(Prison prison)
        {
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
