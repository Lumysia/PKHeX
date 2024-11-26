using System;
using static PKHeX.Core.AbilityPermission;
using static PKHeX.Core.EncounterMutation;

namespace PKHeX.Core;

/// <summary>
/// Settings that can be fed to a <see cref="IEncounterConvertible"/> converter to ensure that the resulting <see cref="PKM"/> meets rough specifications.
/// </summary>
public readonly record struct EncounterCriteria : IFixedNature, IFixedAbilityNumber, IShinyPotential, ILevelRange
{
    /// <summary>
    /// Default criteria with no restrictions (random) for all fields.
    /// </summary>
    public static readonly EncounterCriteria Unrestricted = new();

    /// <summary> End result's gender. </summary>
    /// <remarks> Leave as <see cref="Gender.Random"/> to not restrict gender. </remarks>
    public Gender Gender { get; init; }

    /// <summary> End result's ability numbers permitted. </summary>
    /// <remarks> Leave as <see cref="Any12H"/> to not restrict ability. </remarks>
    public AbilityPermission Ability { get; init; }

    /// <summary> End result's nature. </summary>
    /// <remarks> Leave as <see cref="Nature.Random"/> to not restrict nature. </remarks>
    public Nature Nature { get; init; }

    /// <summary> End result's shininess. </summary>
    /// <remarks> Leave as <see cref="Shiny.Random"/> to not restrict shininess. </remarks>
    public Shiny Shiny { get; init; }

    public sbyte IV_HP  { get; init; }
    public sbyte IV_ATK { get; init; }
    public sbyte IV_DEF { get; init; }
    public sbyte IV_SPA { get; init; }
    public sbyte IV_SPD { get; init; }
    public sbyte IV_SPE { get; init; }

    public byte LevelMin { get; init; }
    public byte LevelMax { get; init; }

    public sbyte HiddenPowerType { get; init; }

    /// <summary> Flexibility for the satisfaction of the criteria. </summary>
    public EncounterMutation Mutations { get; init; }

    public EncounterCriteria()
    {
        Gender = Gender.Random;
        Ability = Any12H;
        Nature = Nature.Random;
        Shiny = Shiny.Random;

        IV_HP = IV_ATK = IV_DEF = IV_SPA = IV_SPD = IV_SPE = RandomIV;
        LevelMin = LevelMax = 0;
        HiddenPowerType = -1;
    }

    public bool IsSatisfiedHiddenPower(uint iv32) => HiddenPower.GetType(iv32) == HiddenPowerType;

    private const sbyte RandomIV = -1;

    public bool IsSpecifiedHiddenPower() => HiddenPowerType != -1;
    public bool IsSpecifiedNature() => Nature != Nature.Random || Mutations.IsComplexNature();
    public bool IsSpecifiedLevelRange() => LevelMax != 0;

    public bool IsSpecifiedAbility() => Ability != Any12H;

    public bool IsSpecifiedIVsAll() => IV_HP != RandomIV
                                   && IV_ATK != RandomIV
                                   && IV_DEF != RandomIV
                                   && IV_SPA != RandomIV
                                   && IV_SPD != RandomIV
                                   && IV_SPE != RandomIV;

    public bool IsSpecifiedIVsAny(out int count) => (count = Convert.ToInt32(IV_HP  != RandomIV)
                                                           + Convert.ToInt32(IV_ATK != RandomIV)
                                                           + Convert.ToInt32(IV_DEF != RandomIV)
                                                           + Convert.ToInt32(IV_SPA != RandomIV)
                                                           + Convert.ToInt32(IV_SPD != RandomIV)
                                                           + Convert.ToInt32(IV_SPE != RandomIV)) != 0;

    public bool IsSatisfiedAbility(int index) => IsSatisfiedAbility(index, Ability);

    private bool IsSatisfiedAbility(int index, AbilityPermission ability) => ability switch
    {
        Any12H     => true,
        Any12      => index < 2  || Mutations.HasFlag(CanAbilityCapsule),
        OnlyFirst  => index == 0 || Mutations.HasFlag(CanAbilityCapsule),
        OnlySecond => index == 1 || Mutations.HasFlag(CanAbilityCapsule),
        OnlyHidden => index == 2 || Mutations.HasFlag(CanAbilityPatch),
        _ => throw new ArgumentOutOfRangeException(nameof(ability), ability, null),
    };

    public bool IsSatisfiedNature(Nature nature)
    {
        if (Mutations.HasFlag(AllowOnlyNeutralNature))
            return nature.IsNeutral();
        if (Nature == Nature.Random)
            return true;
        return nature == Nature || Mutations.HasFlag(CanMintNature);
    }

    public bool IsSatisfiedLevelRange(byte level) => LevelMin <= level && level <= LevelMax;

    /// <summary>
    /// Checks if the IVs are compatible with the encounter's defined IV restrictions.
    /// </summary>
    /// <param name="encounterIVs">Encounter template's IV restrictions. Speed is last!</param>
    /// <returns>True if compatible, false if incompatible.</returns>
    public bool IsIVsCompatibleSpeedLast(Span<int> encounterIVs)
    {
        var IVs = encounterIVs;
        if (!IsSatisfiedIV(IV_HP , IVs[0])) return false;
        if (!IsSatisfiedIV(IV_ATK, IVs[1])) return false;
        if (!IsSatisfiedIV(IV_DEF, IVs[2])) return false;
        if (!IsSatisfiedIV(IV_SPA, IVs[3])) return false;
        if (!IsSatisfiedIV(IV_SPD, IVs[4])) return false;
        if (!IsSatisfiedIV(IV_SPE, IVs[5])) return false;

        return true;
    }

    private bool IsSatisfiedIV(int request, int check)
    {
        if (request >= 30 && Mutations.HasFlag(CanApplyBottleCaps))
            return true; // hyper training possible
        return check == RandomIV || request == RandomIV || request == check;
    }

    /// <inheritdoc cref="GetCriteria(IBattleTemplate, IPersonalInfo, EncounterMutation)"/>
    /// <param name="s">Template data (end result).</param>
    /// <param name="t">Personal table the end result will exist with.</param>
    /// <param name="allowed">Allowed mutations for the encounter.</param>
    public static EncounterCriteria GetCriteria(IBattleTemplate s, IPersonalTable t, EncounterMutation allowed = 0)
    {
        var pi = t.GetFormEntry(s.Species, s.Form);
        return GetCriteria(s, pi, allowed);
    }

    /// <summary>
    /// Creates a new <see cref="EncounterCriteria"/> by loading parameters from the provided <see cref="IBattleTemplate"/>.
    /// </summary>
    /// <param name="s">Template data (end result).</param>
    /// <param name="pi">Personal info the end result will exist with.</param>
    /// <param name="allowed">Allowed mutations for the encounter.</param>
    /// <returns>Initialized criteria data to be passed to generators.</returns>
    public static EncounterCriteria GetCriteria(IBattleTemplate s, IPersonalInfo pi, EncounterMutation allowed = 0) => new()
    {
        Gender = GetGenderPermissions(s.Gender, pi),
        IV_HP  = (sbyte)s.IVs[0],
        IV_ATK = (sbyte)s.IVs[1],
        IV_DEF = (sbyte)s.IVs[2],
        IV_SPE = (sbyte)s.IVs[3],
        IV_SPA = (sbyte)s.IVs[4],
        IV_SPD = (sbyte)s.IVs[5],
        HiddenPowerType = s.HiddenPowerType,
        Mutations = allowed,
        LevelMax = s.Level,

        Ability = GetAbilityPermissions(s.Ability, pi),
        Nature = NatureUtil.GetNature(s.Nature),
        Shiny = s.Shiny ? Shiny.Always : Shiny.Never,
    };

    private static Gender GetGenderPermissions(byte? gender, IGenderDetail pi)
    {
        if (gender is not <= 1)
            return Gender.Random;
        if (pi.IsDualGender)
            return (Gender)gender;
        var g = pi.FixedGender();
        return g <= 1 ? (Gender)g : Gender.Random;
    }

    private static AbilityPermission GetAbilityPermissions(int ability, IPersonalAbility pi)
    {
        var count = pi.AbilityCount;
        if (count < 2 || pi is not IPersonalAbility12 a)
            return Any12;
        var dual = GetAbilityValueDual(ability, a);
        if (count == 2 || pi is not IPersonalAbility12H h) // prior to Gen5
            return dual;
        if (ability == h.AbilityH)
            return dual == Any12 ? Any12H : OnlyHidden;
        return dual;
    }

    private static AbilityPermission GetAbilityValueDual(int ability, IPersonalAbility12 a)
    {
        if (ability == a.Ability1)
            return ability != a.Ability2 ? OnlyFirst : Any12;
        return ability == a.Ability2 ? OnlySecond : Any12;
    }

    /// <summary>
    /// Gets the nature to generate, random if unspecified by the template or criteria.
    /// </summary>
    public Nature GetNature(Nature encValue)
    {
        if ((uint)encValue < 25)
            return encValue;
        return GetNature();
    }

    /// <summary>
    /// Gets the nature to generate, random if unspecified.
    /// </summary>
    public Nature GetNature()
    {
        if (Nature != Nature.Random)
            return Nature;
        var result = (Nature)Util.Rand.Next(25);
        if (Mutations.HasFlag(AllowOnlyNeutralNature))
            return result.ToNeutral();
        return result;
    }

    /// <summary>
    /// Indicates if the <see cref="Gender"/> is specified.
    /// </summary>
    public bool IsSpecifiedGender() => Gender != Gender.Random;

    /// <summary>
    /// Indicates if the requested gender matches the criteria.
    /// </summary>
    public bool IsSatisfiedGender(byte gender) => (Gender)gender == Gender;

    /// <summary>
    /// Gets the gender to generate, random if unspecified by the template or criteria.
    /// </summary>
    public byte GetGender(byte gender, IGenderDetail pkPersonalInfo)
    {
        if ((uint)gender < 3)
            return gender;
        return GetGender(pkPersonalInfo);
    }

    /// <inheritdoc cref="GetGender(byte, IGenderDetail)"/>
    public byte GetGender(Gender gender, IGenderDetail pkPersonalInfo)
    {
        if (gender == Gender.Random)
            return GetGender(pkPersonalInfo);
        return (byte)gender;
    }

    /// <summary>
    /// Gets the gender to generate, random if unspecified.
    /// </summary>
    public byte GetGender(IGenderDetail pkPersonalInfo)
    {
        if (!pkPersonalInfo.IsDualGender)
            return pkPersonalInfo.FixedGender();
        if (pkPersonalInfo.Genderless)
            return 2;
        if (Gender is not Gender.Random)
            return (byte)Gender;
        return pkPersonalInfo.RandomGender();
    }

    /// <summary>
    /// Gets a random ability index (0/1/2) to generate, based off an encounter's <see cref="num"/>.
    /// </summary>
    public int GetAbilityFromNumber(AbilityPermission num)
    {
        if (num.IsSingleValue(out int index)) // fixed number
            return index;

        bool canBeHidden = num.CanBeHidden();
        return GetAbilityIndexPreference(canBeHidden);
    }

    private int GetAbilityIndexPreference(bool canBeHidden = false) => Ability switch
    {
        OnlyFirst => 0,
        OnlySecond => 1,
        OnlyHidden or Any12H when canBeHidden => 2, // hidden allowed
        _ => Util.Rand.Next(2),
    };

    /// <summary>
    /// Applies random IVs without any correlation.
    /// </summary>
    /// <param name="pk">Entity to mutate.</param>
    public void SetRandomIVs(PKM pk) => SetRandomIVs(pk, Util.Rand);

    /// <inheritdoc cref="SetRandomIVs(PKM)"/>
    public void SetRandomIVs(PKM pk, Random rnd)
    {
        pk.IV_HP = IV_HP != RandomIV ? IV_HP : rnd.Next(32);
        pk.IV_ATK = IV_ATK != RandomIV ? IV_ATK : rnd.Next(32);
        pk.IV_DEF = IV_DEF != RandomIV ? IV_DEF : rnd.Next(32);
        pk.IV_SPA = IV_SPA != RandomIV ? IV_SPA : rnd.Next(32);
        pk.IV_SPD = IV_SPD != RandomIV ? IV_SPD : rnd.Next(32);
        pk.IV_SPE = IV_SPE != RandomIV ? IV_SPE : rnd.Next(32);
    }

    /// <summary>
    /// Applies random IVs with a minimum and maximum (bit-shifted >> 1)
    /// </summary>
    /// <param name="pk">Entity to mutate.</param>
    /// <param name="minIV">Minimum IV from GO</param>
    /// <param name="maxIV">Maximum IV from GO</param>
    public void SetRandomIVsGO(PKM pk, int minIV = 0, int maxIV = 15)
    {
        var bareMin = (minIV << 1) | 1;
        var rnd = Util.Rand;
        pk.IV_HP =
              IV_HP  != RandomIV && IV_HP  >= bareMin ? IV_HP  | 1
            : (rnd.Next(minIV, maxIV + 1) << 1) | 1; // hp
        pk.IV_ATK = pk.IV_SPA =
              IV_ATK != RandomIV && IV_ATK >= bareMin ? IV_ATK | 1
            : IV_SPA != RandomIV && IV_SPA >= bareMin ? IV_SPA | 1
            : (rnd.Next(minIV, maxIV + 1) << 1) | 1; // attack
        pk.IV_DEF = pk.IV_SPD =
              IV_DEF != RandomIV && IV_DEF >= bareMin ? IV_DEF | 1
            : IV_SPD != RandomIV && IV_SPD >= bareMin ? IV_SPD | 1
            : (rnd.Next(minIV, maxIV + 1) << 1) | 1; // defense
        pk.IV_SPE =
              IV_SPE != RandomIV ? IV_SPE
            : rnd.Next(32); // speed
    }

    public void SetRandomIVs(PKM pk, int flawless) => SetRandomIVs(pk, flawless, Util.Rand);

    public void SetRandomIVs(PKM pk, int flawless, Random rand)
    {
        Span<int> ivs = [IV_HP, IV_ATK, IV_DEF, IV_SPE, IV_SPA, IV_SPD];
        flawless -= ivs.Count(31);
        int remain = ivs.Count(RandomIV);
        if (flawless > remain)
        {
            // Overwrite specified IVs until we have enough remaining slots.
            while (flawless > remain)
            {
                int index = rand.Next(6);
                if (ivs[index] is RandomIV or 31)
                    continue;
                ivs[index] = RandomIV;
                remain++;
            }
        }

        // Sprinkle in remaining flawless IVs
        while (flawless > 0)
        {
            int index = rand.Next(6);
            if (ivs[index] != RandomIV)
                continue;
            ivs[index] = 31;
            flawless--;
        }
        // Fill in the rest
        for (int i = 0; i < ivs.Length; i++)
        {
            if (ivs[i] == RandomIV)
                ivs[i] = rand.Next(32);
        }
        // Done.
        pk.SetIVs(ivs);
    }

    /// <summary>
    /// Applies random IVs without any correlation.
    /// </summary>
    /// <param name="pk">Entity to mutate.</param>
    /// <param name="template">Template to populate from</param>
    public void SetRandomIVs(PKM pk, in IndividualValueSet template)
    {
        if (!template.IsSpecified)
        {
            SetRandomIVs(pk);
            return;
        }

        pk.IV_HP = Get(template.HP, IV_HP);
        pk.IV_ATK = Get(template.ATK, IV_ATK);
        pk.IV_DEF = Get(template.DEF, IV_DEF);
        pk.IV_SPE = Get(template.SPE, IV_SPE);
        pk.IV_SPA = Get(template.SPA, IV_SPA);
        pk.IV_SPD = Get(template.SPD, IV_SPD);

        static int Get(sbyte template, int request)
        {
            if (template != -1)
                return template;
            if (request != RandomIV)
                return request;
            return Util.Rand.Next(32);
        }
    }

    public bool IsCompatibleIVs(ReadOnlySpan<int> ivs)
    {
        if (ivs.Length != 6)
            return false;
        if (IV_HP != RandomIV && IV_HP != ivs[0])
            return false;
        if (IV_ATK != RandomIV && IV_ATK != ivs[1])
            return false;
        if (IV_DEF != RandomIV && IV_DEF != ivs[2])
            return false;
        if (IV_SPE != RandomIV && IV_SPE != ivs[3])
            return false;
        if (IV_SPA != RandomIV && IV_SPA != ivs[4])
            return false;
        if (IV_SPD != RandomIV && IV_SPD != ivs[5])
            return false;
        return true;
    }

    public bool IsCompatibleIVs(uint iv32)
    {
        if ( IV_HP != RandomIV &&  IV_HP != ((iv32 >> (0 * 5)) & 0x1F))
            return false;
        if (IV_ATK != RandomIV && IV_ATK != ((iv32 >> (1 * 5)) & 0x1F))
            return false;
        if (IV_DEF != RandomIV && IV_DEF != ((iv32 >> (2 * 5)) & 0x1F))
            return false;
        if (IV_SPE != RandomIV && IV_SPE != ((iv32 >> (3 * 5)) & 0x1F))
            return false;
        if (IV_SPA != RandomIV && IV_SPA != ((iv32 >> (4 * 5)) & 0x1F))
            return false;
        if (IV_SPD != RandomIV && IV_SPD != ((iv32 >> (5 * 5)) & 0x1F))
            return false;
        return true;
    }

    public void GetCombinedIVs(out uint iv1, out uint iv2)
    {
        iv1 = (byte)IV_HP | (uint)IV_ATK << 5 | (uint)IV_DEF << 10;
        iv2 = (byte)IV_SPE | (uint)IV_SPA << 5 | (uint)IV_SPD << 10;
    }

    public uint GetCombinedIVs() => (byte)IV_HP
                                  | (uint)IV_ATK << 5
                                  | (uint)IV_DEF << 10
                                  | (uint)IV_SPE << 15
                                  | (uint)IV_SPA << 20
                                  | (uint)IV_SPD << 25;

    public ushort GetCombinedDVs() => (ushort)((byte)IV_SPA | (byte)IV_SPE << 4 | (byte)IV_DEF << 8 | (byte)IV_ATK << 12);

    public bool IsSatisfiedIVs(uint iv32)
    {
        if (!IsSatisfiedIV(IV_HP, (int)((iv32 >> 00) & 0x1F))) return false;
        if (!IsSatisfiedIV(IV_ATK, (int)((iv32 >> 05) & 0x1F))) return false;
        if (!IsSatisfiedIV(IV_DEF, (int)((iv32 >> 10) & 0x1F))) return false;
        if (!IsSatisfiedIV(IV_SPE, (int)((iv32 >> 15) & 0x1F))) return false;
        if (!IsSatisfiedIV(IV_SPA, (int)((iv32 >> 20) & 0x1F))) return false;
        if (!IsSatisfiedIV(IV_SPD, (int)((iv32 >> 25) & 0x1F))) return false;
        return true;
    }
}

[Flags]
public enum EncounterMutation : byte
{
    None = 0,
    CanMintNature = 1 << 0,
    CanApplyBottleCaps = 1 << 1,

    CanAbilityCapsule = 1 << 2,
    CanAbilityPatch = 1 << 3,

    AllowOnlyNeutralNature = 1 << 4,
}

public static class EncounterMutationUtil
{
    public static bool IsComplexNature(this EncounterMutation m) => m.HasFlag(EncounterMutation.AllowOnlyNeutralNature);
}
