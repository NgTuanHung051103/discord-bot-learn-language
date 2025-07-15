using System.ComponentModel;

namespace NTH.DefineEnum
{
    public enum TypeWord
    {
        [Description("Unknown")]
        Unknow = 0,
        [Description("Noun")]
        Noun = 1,
        [Description("Verb")]
        Verb = 2,
        [Description("Adjective")]
        Adjective = 3,
        [Description("Adverb")]
        Adverb = 4,
        [Description("NounPhrase")]
        NounPhrase = 5,
        [Description("Sentence")]
        Sentence = 6,
        [Description("Grammar")]
        Grammar = 7,
        [Description("Paragraph")]
        Paragraph = 8,
    }
}
