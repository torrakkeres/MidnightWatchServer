using System;
using System.Collections;
using System.Collections.Generic;
using Server.Network;
using Server.Engines.Craft;
using Server.Factions;
using Server.Mobiles;
using AMA = Server.Items.ArmorMeditationAllowance;
using AMT = Server.Items.ArmorMaterialType;
using ABT = Server.Items.ArmorBodyType;

namespace Server.Items
{
	public abstract class BaseArmor : Item, IScissorable, IFactionItem, ICraftable, IWearableDurability
	{
		#region Factions
		private FactionItem m_FactionState;

		public FactionItem FactionItemState
		{
			get{ return m_FactionState; }
			set
			{
				m_FactionState = value;

				if ( m_FactionState == null )
					Hue = CraftResources.GetHue( Resource );

				LootType = ( m_FactionState == null ? LootType.Regular : LootType.Blessed );
			}
		}
		#endregion



		/* Armor internals work differently now (Jun 19 2003)
		 * 
		 * The attributes defined below default to -1.
		 * If the value is -1, the corresponding virtual 'Aos/Old' property is used.
		 * If not, the attribute value itself is used. Here's the list:
		 *  - ArmorBase
		 *  - StrBonus
		 *  - DexBonus
		 *  - IntBonus
		 *  - StrReq
		 *  - DexReq
		 *  - IntReq
		 *  - MeditationAllowance
		 */

		// Instance values. These values must are unique to each armor piece.
		private int m_MaxHitPoints;
		private int m_HitPoints;
		private Mobile m_Crafter;
		private ArmorQuality m_Quality;
		private ArmorDurabilityLevel m_Durability;
		private ArmorProtectionLevel m_Protection;
		private CraftResource m_Resource;
		private bool m_Identified, m_PlayerConstructed;
		private int m_PhysicalBonus, m_FireBonus, m_ColdBonus, m_PoisonBonus, m_EnergyBonus;

		private AosAttributes m_AosAttributes;
		private AosArmorAttributes m_AosArmorAttributes;
		private AosSkillBonuses m_AosSkillBonuses;

		// Overridable values. These values are provided to override the defaults which get defined in the individual armor scripts.
		private int m_ArmorBase = -1;
		private int m_StrBonus = -1, m_DexBonus = -1, m_IntBonus = -1;
		private int m_StrReq = -1, m_DexReq = -1, m_IntReq = -1;
		private AMA m_Meditate = (AMA)(-1);


		public virtual bool AllowMaleWearer{ get{ return true; } }
		public virtual bool AllowFemaleWearer{ get{ return true; } }

		public abstract AMT MaterialType{ get; }

		public virtual int RevertArmorBase{ get{ return ArmorBase; } }
		public virtual int ArmorBase{ get{ return 0; } }

		public virtual AMA DefMedAllowance{ get{ return AMA.None; } }
		public virtual AMA AosMedAllowance{ get{ return DefMedAllowance; } }
		public virtual AMA OldMedAllowance{ get{ return DefMedAllowance; } }


		public virtual int AosStrBonus{ get{ return 0; } }
		public virtual int AosDexBonus{ get{ return 0; } }
		public virtual int AosIntBonus{ get{ return 0; } }
		public virtual int AosStrReq{ get{ return 0; } }
		public virtual int AosDexReq{ get{ return 0; } }
		public virtual int AosIntReq{ get{ return 0; } }


		public virtual int OldStrBonus{ get{ return 0; } }
		public virtual int OldDexBonus{ get{ return 0; } }
		public virtual int OldIntBonus{ get{ return 0; } }
		public virtual int OldStrReq{ get{ return 0; } }
		public virtual int OldDexReq{ get{ return 0; } }
		public virtual int OldIntReq{ get{ return 0; } }

		public virtual bool CanFortify{ get{ return true; } }

		public override void OnAfterDuped( Item newItem )
		{
			BaseArmor armor = newItem as BaseArmor;

			if ( armor == null )
				return;

			armor.m_AosAttributes = new AosAttributes( newItem, m_AosAttributes );
			armor.m_AosArmorAttributes = new AosArmorAttributes( newItem, m_AosArmorAttributes );
			armor.m_AosSkillBonuses = new AosSkillBonuses( newItem, m_AosSkillBonuses );
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public AMA MeditationAllowance
		{
			get{ return ( m_Meditate == (AMA)(-1) ? Core.AOS ? AosMedAllowance : OldMedAllowance : m_Meditate ); }
			set{ m_Meditate = value; }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public int BaseArmorRating
		{
			get
			{
				if ( m_ArmorBase == -1 )
					return ArmorBase;
				else
					return m_ArmorBase;
			}
			set
			{ 
				m_ArmorBase = value; Invalidate(); 
			}
		}

		public double BaseArmorRatingScaled
		{
			get
			{
				return ( BaseArmorRating * ArmorScalar );
			}
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public virtual double ArmorRating
		{
			get
			{
				int ar = BaseArmorRating;

				if ( m_Protection != ArmorProtectionLevel.Regular )
					ar += 10 + (5 * (int)m_Protection);

				switch ( m_Resource )
				{
					case CraftResource.DullCopper:		ar += 1; break; // 2 -> 1
					case CraftResource.ShadowIron:		ar += 2; break; // 4 -> 2
					case CraftResource.Copper:			ar += 3; break; // 6 -> 3
					case CraftResource.Bronze:			ar += 4; break; // 8 -> 4
					case CraftResource.Gold:			ar += 1; break; // 10 -> 1
					case CraftResource.Agapite:			ar += 5; break; // 12 -> 5
					case CraftResource.Verite:			ar += 6; break; // 14 -> 6
					case CraftResource.Valorite:		ar += 7; break; // 16 -> 7
					case CraftResource.SpinedLeather:	ar += 12; break; // 10 -> 12
					case CraftResource.HornedLeather:	ar += 15; break; // 13 -> 15
					case CraftResource.BarbedLeather:	ar += 18; break; // 16 -> 18
				}

				ar += -8 + (8 * (int)m_Quality);
				return ScaleArmorByDurability( ar );
			}
		}

		public double ArmorRatingScaled
		{
			get
			{
				return ( ArmorRating * ArmorScalar );
			}
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public int StrBonus
		{
			get{ return ( m_StrBonus == -1 ? Core.AOS ? AosStrBonus : OldStrBonus : m_StrBonus ); }
			set{ m_StrBonus = value; InvalidateProperties(); }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public int DexBonus
		{
			get{ return ( m_DexBonus == -1 ? Core.AOS ? AosDexBonus : OldDexBonus : m_DexBonus ); }
			set{ m_DexBonus = value; InvalidateProperties(); }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public int IntBonus
		{
			get{ return ( m_IntBonus == -1 ? Core.AOS ? AosIntBonus : OldIntBonus : m_IntBonus ); }
			set{ m_IntBonus = value; InvalidateProperties(); }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public int StrRequirement
		{
			get{ return ( m_StrReq == -1 ? Core.AOS ? AosStrReq : OldStrReq : m_StrReq ); }
			set{ m_StrReq = value; InvalidateProperties(); }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public int DexRequirement
		{
			get{ return ( m_DexReq == -1 ? Core.AOS ? AosDexReq : OldDexReq : m_DexReq ); }
			set{ m_DexReq = value; InvalidateProperties(); }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public int IntRequirement
		{
			get{ return ( m_IntReq == -1 ? Core.AOS ? AosIntReq : OldIntReq : m_IntReq ); }
			set{ m_IntReq = value; InvalidateProperties(); }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public bool Identified
		{
			get{ return m_Identified; }
			set{ m_Identified = value; InvalidateProperties(); }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public bool PlayerConstructed
		{
			get{ return m_PlayerConstructed; }
			set{ m_PlayerConstructed = value; }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public CraftResource Resource
		{
			get
			{
				return m_Resource;
			}
			set
			{
				if ( m_Resource != value )
				{
					UnscaleDurability();

					m_Resource = value;

					if (!DefTailoring.IsNonColorable(this.GetType()))
					{
						Hue = CraftResources.GetHue(m_Resource);
					}

					Invalidate();
					InvalidateProperties();

					if ( Parent is Mobile )
						((Mobile)Parent).UpdateResistances();

					ScaleDurability();
				}
			}
		}

		public virtual double ArmorScalar
		{
			get
			{
				int pos = (int)BodyPosition;

				if ( pos >= 0 && pos < m_ArmorScalars.Length )
					return m_ArmorScalars[pos];

				return 1.0;
			}
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public int MaxHitPoints
		{
			get{ return m_MaxHitPoints; }
			set{ m_MaxHitPoints = value; InvalidateProperties(); }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public int HitPoints
		{
			get 
			{
				return m_HitPoints;
			}
			set 
			{
				if ( value != m_HitPoints && MaxHitPoints > 0 )
				{
					m_HitPoints = value;

					if ( m_HitPoints < 0 )
						Delete();
					else if ( m_HitPoints > MaxHitPoints )
						m_HitPoints = MaxHitPoints;

					InvalidateProperties();
				}
			}
		}


		[CommandProperty( AccessLevel.GameMaster )]
		public Mobile Crafter
		{
			get{ return m_Crafter; }
			set{ m_Crafter = value; InvalidateProperties(); }
		}

		
		[CommandProperty( AccessLevel.GameMaster )]
		public ArmorQuality Quality
		{
			get{ return m_Quality; }
			set{ UnscaleDurability(); m_Quality = value; Invalidate(); InvalidateProperties(); ScaleDurability(); }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public ArmorDurabilityLevel Durability
		{
			get{ return m_Durability; }
			set{ UnscaleDurability(); m_Durability = value; ScaleDurability(); InvalidateProperties(); }
		}

		public virtual int ArtifactRarity
		{
			get{ return 0; }
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public ArmorProtectionLevel ProtectionLevel
		{
			get
			{
				return m_Protection;
			}
			set
			{
				if ( m_Protection != value )
				{
					m_Protection = value;

					Invalidate();
					InvalidateProperties();

					if ( Parent is Mobile )
						((Mobile)Parent).UpdateResistances();
				}
			}
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public AosAttributes Attributes
		{
			get{ return m_AosAttributes; }
			set{}
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public AosArmorAttributes ArmorAttributes
		{
			get{ return m_AosArmorAttributes; }
			set{}
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public AosSkillBonuses SkillBonuses
		{
			get{ return m_AosSkillBonuses; }
			set{}
		}

		public int ComputeStatReq( StatType type )
		{
			int v;

			if ( type == StatType.Str )
				v = StrRequirement;
			else if ( type == StatType.Dex )
				v = DexRequirement;
			else
				v = IntRequirement;

			return AOS.Scale( v, 100 - GetLowerStatReq() );
		}

		public int ComputeStatBonus( StatType type )
		{
			if ( type == StatType.Str )
				return StrBonus + Attributes.BonusStr;
			else if ( type == StatType.Dex )
				return DexBonus + Attributes.BonusDex;
			else
				return IntBonus + Attributes.BonusInt;
		}

		[CommandProperty( AccessLevel.GameMaster )]
		public int PhysicalBonus{ get{ return m_PhysicalBonus; } set{ m_PhysicalBonus = value; InvalidateProperties(); } }

		[CommandProperty( AccessLevel.GameMaster )]
		public int FireBonus{ get{ return m_FireBonus; } set{ m_FireBonus = value; InvalidateProperties(); } }

		[CommandProperty( AccessLevel.GameMaster )]
		public int ColdBonus{ get{ return m_ColdBonus; } set{ m_ColdBonus = value; InvalidateProperties(); } }

		[CommandProperty( AccessLevel.GameMaster )]
		public int PoisonBonus{ get{ return m_PoisonBonus; } set{ m_PoisonBonus = value; InvalidateProperties(); } }

		[CommandProperty( AccessLevel.GameMaster )]
		public int EnergyBonus{ get{ return m_EnergyBonus; } set{ m_EnergyBonus = value; InvalidateProperties(); } }

		public virtual int BasePhysicalResistance{ get{ return 0; } }
		public virtual int BaseFireResistance{ get{ return 0; } }
		public virtual int BaseColdResistance{ get{ return 0; } }
		public virtual int BasePoisonResistance{ get{ return 0; } }
		public virtual int BaseEnergyResistance{ get{ return 0; } }

		public override int PhysicalResistance{ get{ return BasePhysicalResistance + GetProtOffset() + GetResourceAttrs().ArmorPhysicalResist + m_PhysicalBonus; } }
		public override int FireResistance{ get{ return BaseFireResistance + GetProtOffset() + GetResourceAttrs().ArmorFireResist + m_FireBonus; } }
		public override int ColdResistance{ get{ return BaseColdResistance + GetProtOffset() + GetResourceAttrs().ArmorColdResist + m_ColdBonus; } }
		public override int PoisonResistance{ get{ return BasePoisonResistance + GetProtOffset() + GetResourceAttrs().ArmorPoisonResist + m_PoisonBonus; } }
		public override int EnergyResistance{ get{ return BaseEnergyResistance + GetProtOffset() + GetResourceAttrs().ArmorEnergyResist + m_EnergyBonus; } }

		public virtual int InitMinHits{ get{ return 0; } }
		public virtual int InitMaxHits{ get{ return 0; } }

		[CommandProperty( AccessLevel.GameMaster )]
		public ArmorBodyType BodyPosition
		{
			get
			{
				switch ( this.Layer )
				{
					default:
					case Layer.Neck:		return ArmorBodyType.Gorget;
					case Layer.TwoHanded:	return ArmorBodyType.Shield;
					case Layer.Gloves:		return ArmorBodyType.Gloves;
					case Layer.Helm:		return ArmorBodyType.Helmet;
					case Layer.Arms:		return ArmorBodyType.Arms;

					case Layer.InnerLegs:
					case Layer.OuterLegs:
					case Layer.Pants:		return ArmorBodyType.Legs;

					case Layer.InnerTorso:
					case Layer.OuterTorso:
					case Layer.Shirt:		return ArmorBodyType.Chest;
				}
			}
		}

		public void DistributeBonuses( int amount )
		{
			for ( int i = 0; i < amount; ++i )
			{
				switch ( Utility.Random( 5 ) )
				{
					case 0: ++m_PhysicalBonus; break;
					case 1: ++m_FireBonus; break;
					case 2: ++m_ColdBonus; break;
					case 3: ++m_PoisonBonus; break;
					case 4: ++m_EnergyBonus; break;
				}
			}

			InvalidateProperties();
		}

		public CraftAttributeInfo GetResourceAttrs()
		{
			CraftResourceInfo info = CraftResources.GetInfo( m_Resource );

			if ( info == null )
				return CraftAttributeInfo.Blank;

			return info.AttributeInfo;
		}

		public int GetProtOffset()
		{
			switch ( m_Protection )
			{
				case ArmorProtectionLevel.Guarding: return 1;
				case ArmorProtectionLevel.Hardening: return 2;
				case ArmorProtectionLevel.Fortification: return 3;
				case ArmorProtectionLevel.Invulnerability: return 4;
			}

			return 0;
		}

		public void UnscaleDurability()
		{
			int scale = 100 + GetDurabilityBonus();

			m_HitPoints = ((m_HitPoints * 100) + (scale - 1)) / scale;
			m_MaxHitPoints = ((m_MaxHitPoints * 100) + (scale - 1)) / scale;
			InvalidateProperties();
		}

		public void ScaleDurability()
		{
			int scale = 100 + GetDurabilityBonus();

			m_HitPoints = ((m_HitPoints * scale) + 99) / 100;
			m_MaxHitPoints = ((m_MaxHitPoints * scale) + 99) / 100;
			InvalidateProperties();
		}

		public int GetDurabilityBonus()
		{
			int bonus = 0;

			if ( m_Quality == ArmorQuality.Exceptional )
				bonus += 20;

			switch ( m_Durability )
			{
				case ArmorDurabilityLevel.Durable: bonus += 20; break;
				case ArmorDurabilityLevel.Substantial: bonus += 50; break;
				case ArmorDurabilityLevel.Massive: bonus += 70; break;
				case ArmorDurabilityLevel.Fortified: bonus += 100; break;
				case ArmorDurabilityLevel.Indestructible: bonus += 120; break;
			}

			if ( Core.AOS )
			{
				bonus += m_AosArmorAttributes.DurabilityBonus;

				CraftResourceInfo resInfo = CraftResources.GetInfo( m_Resource );
				CraftAttributeInfo attrInfo = null;

				if ( resInfo != null )
					attrInfo = resInfo.AttributeInfo;

				if ( attrInfo != null )
					bonus += attrInfo.ArmorDurability;
			}

			return bonus;
		}

		public bool Scissor( Mobile from, Scissors scissors )
		{
			if ( !IsChildOf( from.Backpack ) )
			{
				from.SendLocalizedMessage( 502437 ); // Items you wish to cut must be in your backpack.
				return false;
			}

			CraftSystem system = DefTailoring.CraftSystem;

			CraftItem item = system.CraftItems.SearchFor( GetType() );

			if ( item != null && item.Resources.Count == 1 && item.Resources.GetAt( 0 ).Amount >= 2 )
			{
				try
				{
					Item res = (Item)Activator.CreateInstance( CraftResources.GetInfo( m_Resource ).ResourceTypes[0] );

					ScissorHelper( from, res, m_PlayerConstructed ? (item.Resources.GetAt( 0 ).Amount / 2) : 1 );
					return true;
				}
				catch
				{
				}
			}

			from.SendLocalizedMessage( 502440 ); // Scissors can not be used on that to produce anything.
			return false;
		}

		private static double[] m_ArmorScalars = { 0.07, 0.07, 0.14, 0.15, 0.22, 0.35 };

		public static double[] ArmorScalars
		{
			get
			{
				return m_ArmorScalars;
			}
			set
			{
				m_ArmorScalars = value;
			}
		}

		public static void ValidateMobile( Mobile m )
		{
			for ( int i = m.Items.Count - 1; i >= 0; --i )
			{
				if ( i >= m.Items.Count )
					continue;

				Item item = m.Items[i];

				if ( item is BaseArmor )
				{
					BaseArmor armor = (BaseArmor)item;

					if ( !armor.AllowMaleWearer && !m.Female && m.AccessLevel < AccessLevel.GameMaster )
					{
						if ( armor.AllowFemaleWearer )
							m.SendLocalizedMessage( 1010388 ); // Only females can wear this.
						else
							m.SendMessage( "You may not wear this." );

						m.AddToBackpack( armor );
					}
					else if ( !armor.AllowFemaleWearer && m.Female && m.AccessLevel < AccessLevel.GameMaster )
					{
						if ( armor.AllowMaleWearer )
							m.SendLocalizedMessage( 1063343 ); // Only males can wear this.
						else
							m.SendMessage( "You may not wear this." );

						m.AddToBackpack( armor );
					}
				}
			}
		}

		public int GetLowerStatReq()
		{
			if ( !Core.AOS )
				return 0;

			int v = m_AosArmorAttributes.LowerStatReq;

			CraftResourceInfo info = CraftResources.GetInfo( m_Resource );

			if ( info != null )
			{
				CraftAttributeInfo attrInfo = info.AttributeInfo;

				if ( attrInfo != null )
					v += attrInfo.ArmorLowerRequirements;
			}

			if ( v > 100 )
				v = 100;

			return v;
		}

		public override void OnAdded( object parent )
		{
			if ( parent is Mobile )
			{
				Mobile from = (Mobile)parent;

				from.Delta( MobileDelta.Armor ); // Tell them armor rating has changed
			}
		}

		public virtual double ScaleArmorByDurability( double armor )
		{
			int scale = 100;

			if ( m_MaxHitPoints > 0 && m_HitPoints < m_MaxHitPoints )
				scale = 50 + ((50 * m_HitPoints) / m_MaxHitPoints);

			return ( armor * scale ) / 100;
		}

		protected void Invalidate()
		{
			if ( Parent is Mobile )
				((Mobile)Parent).Delta( MobileDelta.Armor ); // Tell them armor rating has changed
		}

		public BaseArmor( Serial serial ) :  base( serial )
		{
		}

		private static void SetSaveFlag( ref SaveFlag flags, SaveFlag toSet, bool setIf )
		{
			if ( setIf )
				flags |= toSet;
		}

		private static bool GetSaveFlag( SaveFlag flags, SaveFlag toGet )
		{
			return ( (flags & toGet) != 0 );
		}

		[Flags]
		private enum SaveFlag
		{
			None				= 0x00000000,
			Attributes			= 0x00000001,
			ArmorAttributes		= 0x00000002,
			PhysicalBonus		= 0x00000004,
			FireBonus			= 0x00000008,
			ColdBonus			= 0x00000010,
			PoisonBonus			= 0x00000020,
			EnergyBonus			= 0x00000040,
			Identified			= 0x00000080,
			MaxHitPoints		= 0x00000100,
			HitPoints			= 0x00000200,
			Crafter				= 0x00000400,
			Quality				= 0x00000800,
			Durability			= 0x00001000,
			Protection			= 0x00002000,
			Resource			= 0x00004000,
			BaseArmor			= 0x00008000,
			StrBonus			= 0x00010000,
			DexBonus			= 0x00020000,
			IntBonus			= 0x00040000,
			StrReq				= 0x00080000,
			DexReq				= 0x00100000,
			IntReq				= 0x00200000,
			MedAllowance		= 0x00400000,
			SkillBonuses		= 0x00800000,
			PlayerConstructed	= 0x01000000
		}

		public override void Serialize( GenericWriter writer )
		{
			base.Serialize( writer );

			writer.Write( (int) 7 ); // version

			SaveFlag flags = SaveFlag.None;

			SetSaveFlag( ref flags, SaveFlag.Attributes,		!m_AosAttributes.IsEmpty );
			SetSaveFlag( ref flags, SaveFlag.ArmorAttributes,	!m_AosArmorAttributes.IsEmpty );
			SetSaveFlag( ref flags, SaveFlag.PhysicalBonus,		m_PhysicalBonus != 0 );
			SetSaveFlag( ref flags, SaveFlag.FireBonus,			m_FireBonus != 0 );
			SetSaveFlag( ref flags, SaveFlag.ColdBonus,			m_ColdBonus != 0 );
			SetSaveFlag( ref flags, SaveFlag.PoisonBonus,		m_PoisonBonus != 0 );
			SetSaveFlag( ref flags, SaveFlag.EnergyBonus,		m_EnergyBonus != 0 );
			SetSaveFlag( ref flags, SaveFlag.Identified,		m_Identified != false );
			SetSaveFlag( ref flags, SaveFlag.MaxHitPoints,		m_MaxHitPoints != 0 );
			SetSaveFlag( ref flags, SaveFlag.HitPoints,			m_HitPoints != 0 );
			SetSaveFlag( ref flags, SaveFlag.Crafter,			m_Crafter != null );
			SetSaveFlag( ref flags, SaveFlag.Quality,			m_Quality != ArmorQuality.Regular );
			SetSaveFlag( ref flags, SaveFlag.Durability,		m_Durability != ArmorDurabilityLevel.Regular );
			SetSaveFlag( ref flags, SaveFlag.Protection,		m_Protection != ArmorProtectionLevel.Regular );
			SetSaveFlag( ref flags, SaveFlag.Resource,			m_Resource != DefaultResource );
			SetSaveFlag( ref flags, SaveFlag.BaseArmor,			m_ArmorBase != -1 );
			SetSaveFlag( ref flags, SaveFlag.StrBonus,			m_StrBonus != -1 );
			SetSaveFlag( ref flags, SaveFlag.DexBonus,			m_DexBonus != -1 );
			SetSaveFlag( ref flags, SaveFlag.IntBonus,			m_IntBonus != -1 );
			SetSaveFlag( ref flags, SaveFlag.StrReq,			m_StrReq != -1 );
			SetSaveFlag( ref flags, SaveFlag.DexReq,			m_DexReq != -1 );
			SetSaveFlag( ref flags, SaveFlag.IntReq,			m_IntReq != -1 );
			SetSaveFlag( ref flags, SaveFlag.MedAllowance,		m_Meditate != (AMA)(-1) );
			SetSaveFlag( ref flags, SaveFlag.SkillBonuses,		!m_AosSkillBonuses.IsEmpty );
			SetSaveFlag( ref flags, SaveFlag.PlayerConstructed,	m_PlayerConstructed != false );

			writer.WriteEncodedInt( (int) flags );

			if ( GetSaveFlag( flags, SaveFlag.Attributes ) )
				m_AosAttributes.Serialize( writer );

			if ( GetSaveFlag( flags, SaveFlag.ArmorAttributes ) )
				m_AosArmorAttributes.Serialize( writer );

			if ( GetSaveFlag( flags, SaveFlag.PhysicalBonus ) )
				writer.WriteEncodedInt( (int) m_PhysicalBonus );

			if ( GetSaveFlag( flags, SaveFlag.FireBonus ) )
				writer.WriteEncodedInt( (int) m_FireBonus );

			if ( GetSaveFlag( flags, SaveFlag.ColdBonus ) )
				writer.WriteEncodedInt( (int) m_ColdBonus );

			if ( GetSaveFlag( flags, SaveFlag.PoisonBonus ) )
				writer.WriteEncodedInt( (int) m_PoisonBonus );

			if ( GetSaveFlag( flags, SaveFlag.EnergyBonus ) )
				writer.WriteEncodedInt( (int) m_EnergyBonus );

			if ( GetSaveFlag( flags, SaveFlag.MaxHitPoints ) )
				writer.WriteEncodedInt( (int) m_MaxHitPoints );

			if ( GetSaveFlag( flags, SaveFlag.HitPoints ) )
				writer.WriteEncodedInt( (int) m_HitPoints );

			if ( GetSaveFlag( flags, SaveFlag.Crafter ) )
				writer.Write( (Mobile) m_Crafter );

			if ( GetSaveFlag( flags, SaveFlag.Quality ) )
				writer.WriteEncodedInt( (int) m_Quality );

			if ( GetSaveFlag( flags, SaveFlag.Durability ) )
				writer.WriteEncodedInt( (int) m_Durability );

			if ( GetSaveFlag( flags, SaveFlag.Protection ) )
				writer.WriteEncodedInt( (int) m_Protection );

			if ( GetSaveFlag( flags, SaveFlag.Resource ) )
				writer.WriteEncodedInt( (int) m_Resource );

			if ( GetSaveFlag( flags, SaveFlag.BaseArmor ) )
				writer.WriteEncodedInt( (int) m_ArmorBase );

			if ( GetSaveFlag( flags, SaveFlag.StrBonus ) )
				writer.WriteEncodedInt( (int) m_StrBonus );

			if ( GetSaveFlag( flags, SaveFlag.DexBonus ) )
				writer.WriteEncodedInt( (int) m_DexBonus );

			if ( GetSaveFlag( flags, SaveFlag.IntBonus ) )
				writer.WriteEncodedInt( (int) m_IntBonus );

			if ( GetSaveFlag( flags, SaveFlag.StrReq ) )
				writer.WriteEncodedInt( (int) m_StrReq );

			if ( GetSaveFlag( flags, SaveFlag.DexReq ) )
				writer.WriteEncodedInt( (int) m_DexReq );

			if ( GetSaveFlag( flags, SaveFlag.IntReq ) )
				writer.WriteEncodedInt( (int) m_IntReq );

			if ( GetSaveFlag( flags, SaveFlag.MedAllowance ) )
				writer.WriteEncodedInt( (int) m_Meditate );

			if ( GetSaveFlag( flags, SaveFlag.SkillBonuses ) )
				m_AosSkillBonuses.Serialize( writer );
		}

		public override void Deserialize( GenericReader reader )
		{
			base.Deserialize( reader );

			int version = reader.ReadInt();

			switch ( version )
			{
				case 7:
				case 6:
				case 5:
				{
					SaveFlag flags = (SaveFlag)reader.ReadEncodedInt();

					if ( GetSaveFlag( flags, SaveFlag.Attributes ) )
						m_AosAttributes = new AosAttributes( this, reader );
					else
						m_AosAttributes = new AosAttributes( this );

					if ( GetSaveFlag( flags, SaveFlag.ArmorAttributes ) )
						m_AosArmorAttributes = new AosArmorAttributes( this, reader );
					else
						m_AosArmorAttributes = new AosArmorAttributes( this );

					if ( GetSaveFlag( flags, SaveFlag.PhysicalBonus ) )
						m_PhysicalBonus = reader.ReadEncodedInt();

					if ( GetSaveFlag( flags, SaveFlag.FireBonus ) )
						m_FireBonus = reader.ReadEncodedInt();

					if ( GetSaveFlag( flags, SaveFlag.ColdBonus ) )
						m_ColdBonus = reader.ReadEncodedInt();

					if ( GetSaveFlag( flags, SaveFlag.PoisonBonus ) )
						m_PoisonBonus = reader.ReadEncodedInt();

					if ( GetSaveFlag( flags, SaveFlag.EnergyBonus ) )
						m_EnergyBonus = reader.ReadEncodedInt();

					if ( GetSaveFlag( flags, SaveFlag.Identified ) )
						m_Identified = ( version >= 7 || reader.ReadBool() );

					if ( GetSaveFlag( flags, SaveFlag.MaxHitPoints ) )
						m_MaxHitPoints = reader.ReadEncodedInt();

					if ( GetSaveFlag( flags, SaveFlag.HitPoints ) )
						m_HitPoints = reader.ReadEncodedInt();

					if ( GetSaveFlag( flags, SaveFlag.Crafter ) )
						m_Crafter = reader.ReadMobile();

					if ( GetSaveFlag( flags, SaveFlag.Quality ) )
						m_Quality = (ArmorQuality)reader.ReadEncodedInt();
					else
						m_Quality = ArmorQuality.Regular;

					if ( version == 5 && m_Quality == ArmorQuality.Low )
						m_Quality = ArmorQuality.Regular;

					if ( GetSaveFlag( flags, SaveFlag.Durability ) )
					{
						m_Durability = (ArmorDurabilityLevel)reader.ReadEncodedInt();

						if ( m_Durability > ArmorDurabilityLevel.Indestructible )
							m_Durability = ArmorDurabilityLevel.Durable;
					}

					if ( GetSaveFlag( flags, SaveFlag.Protection ) )
					{
						m_Protection = (ArmorProtectionLevel)reader.ReadEncodedInt();

						if ( m_Protection > ArmorProtectionLevel.Invulnerability )
							m_Protection = ArmorProtectionLevel.Defense;
					}

					if ( GetSaveFlag( flags, SaveFlag.Resource ) )
						m_Resource = (CraftResource)reader.ReadEncodedInt();
					else
						m_Resource = DefaultResource;

					if ( m_Resource == CraftResource.None )
						m_Resource = DefaultResource;

					if ( GetSaveFlag( flags, SaveFlag.BaseArmor ) )
						m_ArmorBase = reader.ReadEncodedInt();
					else
						m_ArmorBase = -1;

					if ( GetSaveFlag( flags, SaveFlag.StrBonus ) )
						m_StrBonus = reader.ReadEncodedInt();
					else
						m_StrBonus = -1;

					if ( GetSaveFlag( flags, SaveFlag.DexBonus ) )
						m_DexBonus = reader.ReadEncodedInt();
					else
						m_DexBonus = -1;

					if ( GetSaveFlag( flags, SaveFlag.IntBonus ) )
						m_IntBonus = reader.ReadEncodedInt();
					else
						m_IntBonus = -1;

					if ( GetSaveFlag( flags, SaveFlag.StrReq ) )
						m_StrReq = reader.ReadEncodedInt();
					else
						m_StrReq = -1;

					if ( GetSaveFlag( flags, SaveFlag.DexReq ) )
						m_DexReq = reader.ReadEncodedInt();
					else
						m_DexReq = -1;

					if ( GetSaveFlag( flags, SaveFlag.IntReq ) )
						m_IntReq = reader.ReadEncodedInt();
					else
						m_IntReq = -1;

					if ( GetSaveFlag( flags, SaveFlag.MedAllowance ) )
						m_Meditate = (AMA)reader.ReadEncodedInt();
					else
						m_Meditate = (AMA)(-1);

					if ( GetSaveFlag( flags, SaveFlag.SkillBonuses ) )
						m_AosSkillBonuses = new AosSkillBonuses( this, reader );

					if ( GetSaveFlag( flags, SaveFlag.PlayerConstructed ) )
						m_PlayerConstructed = true;

					break;
				}
				case 4:
				{
					m_AosAttributes = new AosAttributes( this, reader );
					m_AosArmorAttributes = new AosArmorAttributes( this, reader );
					goto case 3;
				}
				case 3:
				{
					m_PhysicalBonus = reader.ReadInt();
					m_FireBonus = reader.ReadInt();
					m_ColdBonus = reader.ReadInt();
					m_PoisonBonus = reader.ReadInt();
					m_EnergyBonus = reader.ReadInt();
					goto case 2;
				}
				case 2:
				case 1:
				{
					m_Identified = reader.ReadBool();
					goto case 0;
				}
				case 0:
				{
					m_ArmorBase = reader.ReadInt();
					m_MaxHitPoints = reader.ReadInt();
					m_HitPoints = reader.ReadInt();
					m_Crafter = reader.ReadMobile();
					m_Quality = (ArmorQuality)reader.ReadInt();
					m_Durability = (ArmorDurabilityLevel)reader.ReadInt();
					m_Protection = (ArmorProtectionLevel)reader.ReadInt();

					AMT mat = (AMT)reader.ReadInt();

					if ( m_ArmorBase == RevertArmorBase )
						m_ArmorBase = -1;

					/*m_BodyPos = (ArmorBodyType)*/reader.ReadInt();

					if ( version < 4 )
					{
						m_AosAttributes = new AosAttributes( this );
						m_AosArmorAttributes = new AosArmorAttributes( this );
					}

					if ( version < 3 && m_Quality == ArmorQuality.Exceptional )
						DistributeBonuses( 6 );

					if ( version >= 2 )
					{
						m_Resource = (CraftResource)reader.ReadInt();
					}
					else
					{
						OreInfo info;

						switch ( reader.ReadInt() )
						{
							default:
							case 0: info = OreInfo.Iron; break;
							case 1: info = OreInfo.DullCopper; break;
							case 2: info = OreInfo.ShadowIron; break;
							case 3: info = OreInfo.Copper; break;
							case 4: info = OreInfo.Bronze; break;
							case 5: info = OreInfo.Gold; break;
							case 6: info = OreInfo.Agapite; break;
							case 7: info = OreInfo.Verite; break;
							case 8: info = OreInfo.Valorite; break;
						}

						m_Resource = CraftResources.GetFromOreInfo( info, mat );
					}

					m_StrBonus = reader.ReadInt();
					m_DexBonus = reader.ReadInt();
					m_IntBonus = reader.ReadInt();
					m_StrReq = reader.ReadInt();
					m_DexReq = reader.ReadInt();
					m_IntReq = reader.ReadInt();

					if ( m_StrBonus == OldStrBonus )
						m_StrBonus = -1;

					if ( m_DexBonus == OldDexBonus )
						m_DexBonus = -1;

					if ( m_IntBonus == OldIntBonus )
						m_IntBonus = -1;

					if ( m_StrReq == OldStrReq )
						m_StrReq = -1;

					if ( m_DexReq == OldDexReq )
						m_DexReq = -1;

					if ( m_IntReq == OldIntReq )
						m_IntReq = -1;

					m_Meditate = (AMA)reader.ReadInt();

					if ( m_Meditate == OldMedAllowance )
						m_Meditate = (AMA)(-1);

					if ( m_Resource == CraftResource.None )
					{
						if ( mat == ArmorMaterialType.Studded || mat == ArmorMaterialType.Leather )
							m_Resource = CraftResource.RegularLeather;
						else if ( mat == ArmorMaterialType.Spined )
							m_Resource = CraftResource.SpinedLeather;
						else if ( mat == ArmorMaterialType.Horned )
							m_Resource = CraftResource.HornedLeather;
						else if ( mat == ArmorMaterialType.Barbed )
							m_Resource = CraftResource.BarbedLeather;
						else
							m_Resource = CraftResource.Iron;
					}

					if ( m_MaxHitPoints == 0 && m_HitPoints == 0 )
						m_HitPoints = m_MaxHitPoints = Utility.RandomMinMax( InitMinHits, InitMaxHits );

					break;
				}
			}

			if ( m_AosSkillBonuses == null )
				m_AosSkillBonuses = new AosSkillBonuses( this );

			if ( Core.AOS && Parent is Mobile )
				m_AosSkillBonuses.AddTo( (Mobile)Parent );

			int strBonus = ComputeStatBonus( StatType.Str );
			int dexBonus = ComputeStatBonus( StatType.Dex );
			int intBonus = ComputeStatBonus( StatType.Int );

			if ( Parent is Mobile && (strBonus != 0 || dexBonus != 0 || intBonus != 0) )
			{
				Mobile m = (Mobile)Parent;

				string modName = Serial.ToString();

				if ( strBonus != 0 )
					m.AddStatMod( new StatMod( StatType.Str, modName + "Str", strBonus, TimeSpan.Zero ) );

				if ( dexBonus != 0 )
					m.AddStatMod( new StatMod( StatType.Dex, modName + "Dex", dexBonus, TimeSpan.Zero ) );

				if ( intBonus != 0 )
					m.AddStatMod( new StatMod( StatType.Int, modName + "Int", intBonus, TimeSpan.Zero ) );
			}

			if ( Parent is Mobile )
				((Mobile)Parent).CheckStatTimers();

			if ( version < 7 )
				m_PlayerConstructed = true; // we don't know, so, assume it's crafted
		}

		public virtual CraftResource DefaultResource{ get{ return CraftResource.Iron; } }

		public BaseArmor( int itemID ) :  base( itemID )
		{
			m_Quality = ArmorQuality.Regular;
			m_Durability = ArmorDurabilityLevel.Regular;
			m_Crafter = null;

			m_Resource = DefaultResource;
			Hue = CraftResources.GetHue( m_Resource );

			m_HitPoints = m_MaxHitPoints = Utility.RandomMinMax( InitMinHits, InitMaxHits );

			this.Layer = (Layer)ItemData.Quality;

			m_AosAttributes = new AosAttributes( this );
			m_AosArmorAttributes = new AosArmorAttributes( this );
			m_AosSkillBonuses = new AosSkillBonuses( this );
		}

		public override bool AllowSecureTrade( Mobile from, Mobile to, Mobile newOwner, bool accepted )
		{
			if ( !Ethics.Ethic.CheckTrade( from, to, newOwner, this ) )
				return false;

			return base.AllowSecureTrade( from, to, newOwner, accepted );
		}

		public virtual Race RequiredRace { get { return null; } }

		public override bool CanEquip( Mobile from )
		{
			if( !Ethics.Ethic.CheckEquip( from, this ) )
				return false;

			if( from is PlayerMobile && !((PlayerMobile)from).Undead && LootType == LootType.Cursed )
			{
				from.SendMessage( "Mortals cannot equip the armour of the undead." );
				return false;
			}

			if( from.AccessLevel < AccessLevel.GameMaster )
			{
				if( !AllowMaleWearer && !from.Female )
				{
					if( AllowFemaleWearer )
						from.SendLocalizedMessage( 1010388 ); // Only females can wear this.
					else
						from.SendMessage( "You may not wear this." );

					return false;
				}
				else if( !AllowFemaleWearer && from.Female )
				{
					if( AllowMaleWearer )
						from.SendLocalizedMessage( 1063343 ); // Only males can wear this.
					else
						from.SendMessage( "You may not wear this." );

					return false;
				}
				else
				{
					int strBonus = ComputeStatBonus( StatType.Str ), strReq = ComputeStatReq( StatType.Str );
					int dexBonus = ComputeStatBonus( StatType.Dex ), dexReq = ComputeStatReq( StatType.Dex );
					int intBonus = ComputeStatBonus( StatType.Int ), intReq = ComputeStatReq( StatType.Int );

					if( from.Dex < dexReq || (from.Dex + dexBonus) < 1 )
					{
						from.SendLocalizedMessage( 502077 ); // You do not have enough dexterity to equip this item.
						return false;
					}
					else if( from.Str < strReq || (from.Str + strBonus) < 1 )
					{
						from.SendLocalizedMessage( 500213 ); // You are not strong enough to equip that.
						return false;
					}
					else if( from.Int < intReq || (from.Int + intBonus) < 1 )
					{
						from.SendMessage( "You are not smart enough to equip that." );
						return false;
					}
				}
			}

			return base.CanEquip( from );
		}

		public override bool CheckPropertyConfliction( Mobile m )
		{
			if ( base.CheckPropertyConfliction( m ) )
				return true;

			if ( Layer == Layer.Pants )
				return ( m.FindItemOnLayer( Layer.InnerLegs ) != null );

			if ( Layer == Layer.Shirt )
				return ( m.FindItemOnLayer( Layer.InnerTorso ) != null );

			return false;
		}

		public override bool OnEquip( Mobile from )
		{
			from.CheckStatTimers();

			int strBonus = ComputeStatBonus( StatType.Str );
			int dexBonus = ComputeStatBonus( StatType.Dex );
			int intBonus = ComputeStatBonus( StatType.Int );

			if ( strBonus != 0 || dexBonus != 0 || intBonus != 0 )
			{
				string modName = this.Serial.ToString();

				if ( strBonus != 0 )
					from.AddStatMod( new StatMod( StatType.Str, modName + "Str", strBonus, TimeSpan.Zero ) );

				if ( dexBonus != 0 )
					from.AddStatMod( new StatMod( StatType.Dex, modName + "Dex", dexBonus, TimeSpan.Zero ) );

				if ( intBonus != 0 )
					from.AddStatMod( new StatMod( StatType.Int, modName + "Int", intBonus, TimeSpan.Zero ) );
			}

			return base.OnEquip( from );
		}

		public override void OnRemoved( object parent )
		{
			if ( parent is Mobile )
			{
				Mobile m = (Mobile)parent;
				string modName = this.Serial.ToString();

				m.RemoveStatMod( modName + "Str" );
				m.RemoveStatMod( modName + "Dex" );
				m.RemoveStatMod( modName + "Int" );

				if ( Core.AOS )
					m_AosSkillBonuses.Remove();

				((Mobile)parent).Delta( MobileDelta.Armor ); // Tell them armor rating has changed
				m.CheckStatTimers();
			}

			base.OnRemoved( parent );
		}

		public virtual int OnHit( BaseWeapon weapon, int damageTaken )
		{
			double fraction = ArmorRating / 100;
			int Absorbed = (int)(damageTaken * fraction);
			damageTaken -= Absorbed;

			//Console.Error.WriteLine( "     AR: " + ArmorRating );
			//Console.Error.WriteLine( "     armour absorbed: " + Absorbed );

			if ( damageTaken < 0 ) 
				damageTaken = 0;

			if ( Absorbed < 2 )
				Absorbed = 2;

			if ( 25 > Utility.Random( 100 ) ) // 25% chance to lower durability
			{
				int wear;

				if ( weapon.Type == WeaponType.Bashing )
					wear = Absorbed / 4;
				else
					wear = Utility.Random( 2 );

				if ( wear > 0 && m_MaxHitPoints > 0 )
				{
					if ( m_HitPoints >= wear )
					{
						HitPoints -= wear;
						wear = 0;
					}
					else
					{
						wear -= HitPoints;
						HitPoints = 0;
					}

					if ( wear > 0 )
					{
						if ( m_MaxHitPoints > wear )
						{
							MaxHitPoints -= wear;

							if ( Parent is Mobile )
								((Mobile)Parent).LocalOverheadMessage( MessageType.Regular, 0x3B2, 1061121 ); // Your equipment is severely damaged.
						}
						else
						{
							Delete();
						}
					}
				}
			}

			return damageTaken;
		}

		private string GetNameString()
		{
			string name = this.Name;

			if ( name == null )
				name = String.Format( "#{0}", LabelNumber );

			return name;
		}

		[Hue, CommandProperty( AccessLevel.GameMaster )]
		public override int Hue
		{
			get{ return base.Hue; }
			set{ base.Hue = value; InvalidateProperties(); }
		}

		public override void AddNameProperty( ObjectPropertyList list )
		{
			int oreType;

			switch ( m_Resource )
			{
				case CraftResource.DullCopper:		oreType = 1053108; break; // dull copper
				case CraftResource.ShadowIron:		oreType = 1053107; break; // shadow iron
				case CraftResource.Copper:			oreType = 1053106; break; // copper
				case CraftResource.Bronze:			oreType = 1053105; break; // bronze
				case CraftResource.Gold:			oreType = 1053104; break; // golden
				case CraftResource.Agapite:			oreType = 1053103; break; // agapite
				case CraftResource.Verite:			oreType = 1053102; break; // verite
				case CraftResource.Valorite:		oreType = 1053101; break; // valorite
				case CraftResource.SpinedLeather:	oreType = 1061118; break; // spined
				case CraftResource.HornedLeather:	oreType = 1061117; break; // horned
				case CraftResource.BarbedLeather:	oreType = 1061116; break; // barbed
				case CraftResource.RedScales:		oreType = 1060814; break; // red
				case CraftResource.YellowScales:	oreType = 1060818; break; // yellow
				case CraftResource.BlackScales:		oreType = 1060820; break; // black
				case CraftResource.GreenScales:		oreType = 1060819; break; // green
				case CraftResource.WhiteScales:		oreType = 1060821; break; // white
				case CraftResource.BlueScales:		oreType = 1060815; break; // blue
				default: oreType = 0; break;
			}

			if ( m_Quality == ArmorQuality.Exceptional )
			{
				if ( oreType != 0 )
					list.Add( 1053100, "#{0}\t{1}", oreType, GetNameString() ); // exceptional ~1_oretype~ ~2_armortype~
				else
					list.Add( 1050040, GetNameString() ); // exceptional ~1_ITEMNAME~
			}
			else
			{
				if ( oreType != 0 )
					list.Add( 1053099, "#{0}\t{1}", oreType, GetNameString() ); // ~1_oretype~ ~2_armortype~
				else if ( Name == null )
					list.Add( LabelNumber );
				else
					list.Add( Name );
			}
		}

		public override bool AllowEquipedCast( Mobile from )
		{
			if ( base.AllowEquipedCast( from ) )
				return true;

			return ( m_AosAttributes.SpellChanneling != 0 );
		}

		public virtual int GetLuckBonus()
		{
			CraftResourceInfo resInfo = CraftResources.GetInfo( m_Resource );

			if ( resInfo == null )
				return 0;

			CraftAttributeInfo attrInfo = resInfo.AttributeInfo;

			if ( attrInfo == null )
				return 0;

			return attrInfo.ArmorLuck;
		}

		public override void GetProperties( ObjectPropertyList list )
		{
			base.GetProperties( list );
			base.AddResistanceProperties( list );
		}

		public override void OnSingleClick( Mobile from )
		{
			List<EquipInfoAttribute> attrs = new List<EquipInfoAttribute>();

			if ( DisplayLootType )
			{
				if ( LootType == LootType.Blessed )
					attrs.Add( new EquipInfoAttribute( 1038021 ) ); // blessed
				else if ( LootType == LootType.Cursed )
					attrs.Add( new EquipInfoAttribute( 1049643 ) ); // cursed
			}

			#region Factions
			if ( m_FactionState != null )
				attrs.Add( new EquipInfoAttribute( 1041350 ) ); // faction item
			#endregion

			if ( m_Quality == ArmorQuality.Exceptional )
				attrs.Add( new EquipInfoAttribute( 1018305 - (int)m_Quality ) );

			if ( m_Identified || from.AccessLevel >= AccessLevel.GameMaster)
			{
				if ( m_Durability != ArmorDurabilityLevel.Regular )
					attrs.Add( new EquipInfoAttribute( 1038000 + (int)m_Durability ) );

				if ( m_Protection > ArmorProtectionLevel.Regular && m_Protection <= ArmorProtectionLevel.Invulnerability )
					attrs.Add( new EquipInfoAttribute( 1038005 + (int)m_Protection ) );
			}
			else if ( m_Durability != ArmorDurabilityLevel.Regular || (m_Protection > ArmorProtectionLevel.Regular && m_Protection <= ArmorProtectionLevel.Invulnerability) )
				attrs.Add( new EquipInfoAttribute( 1038000 ) ); // Unidentified

			int number;

			if ( Name == null )
			{
				number = LabelNumber;
			}
			else
			{
				this.LabelTo( from, Name );
				number = 1041000;
			}

			if ( attrs.Count == 0 && Crafter == null && Name != null )
				return;

			EquipmentInfo eqInfo = new EquipmentInfo( number, m_Crafter, false, attrs.ToArray() );

			from.Send( new DisplayEquipmentInfo( this, eqInfo ) );
		}

		#region ICraftable Members

		public int OnCraft( int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool, CraftItem craftItem, int resHue )
		{
			Quality = (ArmorQuality)quality;

			if ( makersMark )
				Crafter = from;

			Type resourceType = typeRes;

			if ( resourceType == null )
				resourceType = craftItem.Resources.GetAt( 0 ).ItemType;

			Resource = CraftResources.GetFromType( resourceType );
			PlayerConstructed = true;

			CraftContext context = craftSystem.GetContext( from );

			if ( context != null && context.DoNotColor )
				Hue = 0;

			if( Quality == ArmorQuality.Exceptional )
			{
				DistributeBonuses( 14 );
			}

			return quality;
		}

		#endregion
	}
}