using System;
using Server.Targeting;
using Server.Items;
using Server.Network;
using Server.Multis;

namespace Server.SkillHandlers
{
	public class Hiding
	{
		private static bool m_CombatOverride;

		public static bool CombatOverride
		{
			get{ return m_CombatOverride; }
			set{ m_CombatOverride = value; }
		}

		public static void Initialize()
		{
			SkillInfo.Table[21].Callback = new SkillUseCallback( OnUse );
		}

		public static TimeSpan OnUse( Mobile m )
		{
			if ( m.Spell != null )
			{
				m.SendLocalizedMessage( 501238 ); // You are busy doing something else and cannot hide.
				return TimeSpan.FromSeconds( 1.0 );
			}

			if ( Core.ML && m.Target != null )
			{
				Targeting.Target.Cancel( m );
			}

			//int range = 18 - (int)(m.Skills[SkillName.Hiding].Value / 10);
			int range = Math.Min( (int)((100 - m.Skills[SkillName.Hiding].Value)/2) + 8, 18 );	//Cap of 18 not OSI-exact, intentional difference

			bool badCombat = ( !m_CombatOverride && m.Combatant != null && m.InRange( m.Combatant.Location, range ) && m.Combatant.InLOS( m ) );
			bool ok = ( !badCombat /*&& m.CheckSkill( SkillName.Hiding, 0.0 - bonus, 100.0 - bonus )*/ );

			if ( ok )
			{
				if ( !m_CombatOverride )
				{
					foreach ( Mobile check in m.GetMobilesInRange( range ) )
					{
						if ( check.InLOS( m ) && check.Combatant == m )
						{
							badCombat = true;
							ok = false;
							break;
						}
					}
				}

				ok = ( !badCombat && m.CheckSkill( SkillName.Hiding, 0.0, 100.0 ) );
			}

			if ( badCombat )
			{
				m.RevealingAction();

				m.LocalOverheadMessage( MessageType.Regular, 0x22, 501237 ); // You can't seem to hide right now.

				return TimeSpan.FromSeconds( 1.0 );
			}
			else 
			{
				if ( ok )
				{
					m.Hidden = true;
					m.Warmode = false;
					m.LocalOverheadMessage( MessageType.Regular, 0x1F4, 501240 ); // You have hidden yourself well.
				}
				else
				{
					m.RevealingAction();

					m.LocalOverheadMessage( MessageType.Regular, 0x22, 501241 ); // You can't seem to hide here.
				}

				return TimeSpan.FromSeconds( 10.0 );
			}
		}
	}
}