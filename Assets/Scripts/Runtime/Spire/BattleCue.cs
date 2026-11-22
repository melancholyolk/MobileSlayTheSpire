namespace Spire
{
    public enum BattleCueKind { Draw, Shuffle, Play, Discard, Exhaust, Attack, Hit, Guard, Skill, Death, Victory, Heal }
    public enum CardOrigin { Automatic, Hand, DrawPile, DiscardPile, ExhaustPile, Actor, PlayArea }
    public readonly struct BattleCue
    {
        public readonly BattleCueKind Kind;
        // Heroes use seats 0..3. Enemies use -1-index.
        public readonly int Actor, Target, ArtId, Amount;
        public readonly CardOrigin Source;
        public readonly string CardId;
        public readonly int HandIndex;
        public readonly int AttackId,BodyPart;
        // Allows an enemy or generated card to use another owner's artwork.
        public readonly int? ArtOwner;
        public BattleCue(BattleCueKind kind,int actor,int target=0,int artId=0,int amount=0,CardOrigin source=CardOrigin.Automatic,int? artOwner=null,string cardId=null,int handIndex=-1,int attackId=0,int bodyPart=-1)
        { Kind=kind; Actor=actor; Target=target; ArtId=artId; Amount=amount; Source=source; ArtOwner=artOwner; CardId=cardId; HandIndex=handIndex; AttackId=attackId; BodyPart=bodyPart; }
    }
}
