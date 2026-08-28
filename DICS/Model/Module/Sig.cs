using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using HashCode = System.HashCode;

namespace DICS
{
    public record Sig(ArraySegment<Key> Args)
    {
        private static readonly Sig Empty = new(new Key[] { });

        public virtual bool Equals(Sig? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Args.SequenceEqual(other.Args);
        }

        public Sig RenameArgs(string?[] names)
        {
            Debug.Assert(names.Length == Args.Count);
            return Of(Args.Zip(names.ToList(), (a, n) => a.Rename(n)).ToArray());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Sig Of(params Key[] args)
        {
            return Of((ArraySegment<Key>)args);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Sig Of(ArraySegment<Key> args)
        {
            // var bad = args.Select((a, i) => (a, i)).IndexBy(a => a.a).Where(kv => kv.Value.Count > 1).ToArray();
            // if (bad.Any())
            //     throw new DicsPlanningException(
            //         $"Signature has duplicating arguments:\n{bad.Select(a => a.Key).ToList().NiceList()}");
            return new Sig(args);
        }

        public static Sig GetEmpty()
        {
            return Empty;
        }

        public override int GetHashCode()
        {
            return Args.Aggregate(0, (currentHash, score) => HashCode.Combine(currentHash, score));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Sig ExtendUnchecked(params Key[] keys)
        {
            return new Sig(Args.Concat(keys).ToArray());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Sig AppendUnchecked(Sig other)
        {
            return ExtendUnchecked(other.Args.ToArray());
        }

        public Sig Slice(int shift)
        {
            return new Sig(Args.Slice(shift));
        }

        public Sig Slice(int shift, int count)
        {
            return new Sig(Args.Slice(shift, count));
        }

        public void Deconstruct(out ArraySegment<Key> args)
        {
            args = Args;
        }
    }
}