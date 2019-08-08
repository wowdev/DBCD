using DBCD.Providers;
using DBCD.IO.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace DBCD.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var githubDBDProvider = new GithubDBDProvider();
            var testDBDProvider = new TestDBDProvider();
            var dbcProvider = new TestDBCProvider(@"C:\Users\TomSpearman\Downloads\dbfilesclient");

            /*
            "C:\\Users\\TomSpearman\\Downloads\\dbfilesclient\\item.db2"
            "C:\\Users\\TomSpearman\\Downloads\\dbfilesclient\\spell.db2"
            "C:\\Users\\TomSpearman\\Downloads\\dbfilesclient\\spelleffect.db2"
            "C:\\Users\\TomSpearman\\Downloads\\dbfilesclient\\spellname.db2"
            */

            //DBCD dbcd = new DBCD(dbcProvider, githubDBDProvider);
            //IDBCDStorage storage = dbcd.Load("Map");

            //var fucked = new System.Collections.Generic.List<string>();
            //foreach (var file in Directory.EnumerateFiles(@"C:\Users\TomSpearman\Downloads\dbfilesclient"))
            //{
            //    try
            //    {
            //        DBCD dbcd = new DBCD(dbcProvider, testDBDProvider);
            //        IDBCDStorage storage = dbcd.Load(Path.GetFileNameWithoutExtension(file));
            //    }
            //    catch
            //    {
            //        fucked.Add(file);
            //    }
            //}

            //DBCD dbcd = new DBCD(dbcProvider, githubDBDProvider);
            //IDBCDStorage storage = dbcd.Load("Creature");

            //IDBCDStorage storage = dbcd.Load("LockType", "1.12.1.5875", Locale.EnUS);

            var fields = typeof(SpellVisualEffectNameRec).GetFields();

            DBCD.IO.DBReader reader = new DBCD.IO.DBReader("SpellVisualEffectName.dbc");
            var recs = reader.GetRecords<SpellVisualEffectNameRec>();
            var val = recs.Values.Where(x => x.Flags > 0).ToArray();

        }

        class SpellVisualEffectNameRec
        {
            [Index]
            public int Id;
            public string FileName;
            public UNITEFFECTSPECIALS[] SpecialID;
            public int SpecialAttachPoint;
            public float AreaEffectSize;
            public Flags Flags;          
        }

        [Flags]
        enum Flags : uint
        {
            ReleaseDeathHolds = 1,
            Unknown = 2,
            OneShotEndHandler = 4,
            UnitEffectIsAuraWorldObject = 8
        }

        enum UNITEFFECTSPECIALS : uint
        {
            SPECIALEFFECT_LOOTART = 0x0,
            SPECIALEFFECT_LEVELUP = 0x1,
            SPECIALEFFECT_FOOTSTEPSPRAYSNOW = 0x2,
            SPECIALEFFECT_FOOTSTEPSPRAYSNOWWALK = 0x3,
            SPECIALEFFECT_FOOTSTEPDIRT = 0x4,
            SPECIALEFFECT_FOOTSTEPDIRTWALK = 0x5,
            SPECIALEFFECT_COLDBREATH = 0x6,
            SPECIALEFFECT_UNDERWATERBUBBLES = 0x7,
            SPECIALEFFECT_COMBATBLOODSPURTFRONT = 0x8,
            SPECIALEFFECT_UNUSED = 0x9,
            SPECIALEFFECT_COMBATBLOODSPURTBACK = 0xA,
            SPECIALEFFECT_HITSPLATPHYSICALSMALL = 0xB,
            SPECIALEFFECT_HITSPLATPHYSICALBIG = 0xC,
            SPECIALEFFECT_HITSPLATHOLYSMALL = 0xD,
            SPECIALEFFECT_HITSPLATHOLYBIG = 0xE,
            SPECIALEFFECT_HITSPLATFIRESMALL = 0xF,
            SPECIALEFFECT_HITSPLATFIREBIG = 0x10,
            SPECIALEFFECT_HITSPLATNATURESMALL = 0x11,
            SPECIALEFFECT_HITSPLATNATUREBIG = 0x12,
            SPECIALEFFECT_HITSPLATFROSTSMALL = 0x13,
            SPECIALEFFECT_HITSPLATFROSTBIG = 0x14,
            SPECIALEFFECT_HITSPLATSHADOWSMALL = 0x15,
            SPECIALEFFECT_HITSPLATSHADOWBIG = 0x16,
            SPECIALEFFECT_COMBATBLOODSPURTFRONTLARGE = 0x17,
            SPECIALEFFECT_COMBATBLOODSPURTBACKLARGE = 0x18,
            SPECIALEFFECT_FIZZLEPHYSICAL = 0x19,
            SPECIALEFFECT_FIZZLEHOLY = 0x1A,
            SPECIALEFFECT_FIZZLEFIRE = 0x1B,
            SPECIALEFFECT_FIZZLENATURE = 0x1C,
            SPECIALEFFECT_FIZZLEFROST = 0x1D,
            SPECIALEFFECT_FIZZLESHADOW = 0x1E,
            SPECIALEFFECT_COMBATBLOODSPURTGREENFRONT = 0x1F,
            SPECIALEFFECT_COMBATBLOODSPURTGREENFRONTLARGE = 0x20,
            SPECIALEFFECT_COMBATBLOODSPURTGREENBACK = 0x21,
            SPECIALEFFECT_COMBATBLOODSPURTGREENBACKLARGE = 0x22,
            SPECIALEFFECT_FOOTSTEPSPRAYWATER = 0x23,
            SPECIALEFFECT_FOOTSTEPSPRAYWATERWALK = 0x24,
            SPECIALEFFECT_CHARACTERSHAPESHIFT = 0x25,
            SPECIALEFFECT_COMBATBLOODSPURTBLACKFRONT = 0x26,
            SPECIALEFFECT_COMBATBLOODSPURTBLACKFRONTLARGE = 0x27,
            SPECIALEFFECT_COMBATBLOODSPURTBLACKBACK = 0x28,
            SPECIALEFFECT_COMBATBLOODSPURTBLACKBACKLARGE = 0x29,
            SPECIALEFFECT_RES_EFFECT = 0x2A,
            NUM_UNITEFFECTSPECIALS = 0x2B,
            SPECIALEFFECT_NONE = 0xFFFFFFFF,
        };
    }
}
