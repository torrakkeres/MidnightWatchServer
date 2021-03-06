using System;
using Server;

namespace Server.Items
{
	public class LandMine : BaseMine
	{
		public override int AttackMessage{ get{ return 1010543; } } // You are enveloped in an explosion of fire!
		public override int DisarmMessage{ get{ return 1010539; } } // You carefully remove the pressure trigger and disable the trap.
		public override int EffectSound{ get{ return 0x307; } }
		public override int MessageHue{ get{ return 0x78; } }

		public override void DoVisibleEffect()
		{
			Effects.SendLocationEffect( GetWorldLocation(), Map, 0x36BD, 15, 10 );
		}

		public override void DoAttackEffect( Mobile m )
		{
			m.Damage( Utility.RandomMinMax( 50, 70 ), m );
		}

		[Constructable]
		public LandMine() : this( null )
		{
		}

		public LandMine( Mobile m ) : base( m, 0x11C1 )
		{
			Name = "a land mine";
		}

		public LandMine( Serial serial ) : base( serial )
		{
		}

		public override void Serialize( GenericWriter writer )
		{
			base.Serialize( writer );

			writer.Write( (int) 0 ); // version
		}

		public override void Deserialize( GenericReader reader )
		{
			base.Deserialize( reader );

			int version = reader.ReadInt();
		}
	}

	public class LandMineDeed : MineDeed
	{
		public override Type TrapType{ get{ return typeof( LandMine ); } }
		public override int LabelNumber{ get{ return 1044603; } } // faction explosion trap deed

		[Constructable]
		public LandMineDeed() : base( 0x36D2 )
		{
			Name = "a land mine";
		}
		
		public LandMineDeed( Serial serial ) : base( serial )
		{
		}

		public override void Serialize( GenericWriter writer )
		{
			base.Serialize( writer );

			writer.Write( (int) 0 ); // version
		}

		public override void Deserialize( GenericReader reader )
		{
			base.Deserialize( reader );
			int version = reader.ReadInt();
		}
	}
}