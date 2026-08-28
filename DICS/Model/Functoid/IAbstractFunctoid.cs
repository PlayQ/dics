using System;

namespace DICS
{
    public interface IAbstractFunctoid<TSelf> where TSelf : IAbstractFunctoid<TSelf>
    {
        public Sig Signature();

        public Type Underlying();

        public TSelf RenameArgs(string?[] names);
    }
}