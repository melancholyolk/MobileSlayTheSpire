namespace Spire
{
    public enum BattleCueKind { Draw, Shuffle, Play, Discard, Exhaust, Attack, Hit, Guard, Skill, Death, Victory }
    public readonly struct BattleCue
    {
        public readonly BattleCueKind Kind;
        // Heroes use seats 0..3. Enemies use -1-index.
        public readonly int Actor, Target, ArtId, Amount;
        public BattleCue(BattleCueKind kind,int actor,int target=0,int artId=0,int amount=0)
        { Kind=kind; Actor=actor; Target=target; ArtId=artId; Amount=amount; }
    }
}
