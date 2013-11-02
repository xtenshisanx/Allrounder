**Allrounder Custom Rotation for ExileBuddy**

Hey Everyboddy,
this custom rotation is designed and written by xTenshiSanx (me).
It provides a generic and individual combatrotation for everybody without changing anything directly in the code.
The best is so nobody could destroy the code accidentally.

**Install**

Just Download these Github and Unzip it in your Exilebuddy folder.
After the first start with the character of your choise the bot will autocreate a template file for your char

**Rotation Config**

The Rotation is Characterspecific so its not a general rotation, you dont have to change the rotation for every character.
Just setup the Settingsfile in the Settings\Allrounder folder CHARACTERNAME.cfg

The working variables for the settingsfile are
- (int) PotHealth
- *Min Health for using HealthPotion (Percent) //default: 70*
- (int) PotMana
- *Min Mana for using ManaPotion (Percent) //default: 50*
- (int) FightDistance
- *Minimal Distance to fight a enemy //default: 50*
- (string) Name
- *Skillname*
- (int) MinManaPercent
- *Min Mana for using this skill (Percent)*
- (int) MinLifePercent
- *Min Life for using this skill (Percent)*
- (int) MinEnemylifePercent
- *Min Life of Enemy for using this skill (Percent) maybe for Crushing Blow attacks*
- (int) Mobsarround_Distance
- *Distance for searching mobs around target*
- (int) Mobsarround_Count
- *Min Count of mobs in range of Target*
- (int) Mobsarround_Target
- *0 MainTarget // 1 Me*
- (int) EnemyDistance
- *Min Distance for using this Skill*
- (int) MaxCount
- *Max Count of Minions/Traps*
- (bool) CheckForMobsarround
- *Set this to True for searching mobs around target*
- (bool) OnlyBosses
- *BossSkill (Heavy Strike for example)*
- (bool) IsTrap
- *Is Skill a Trap*
- (bool) IsSummon
- *Is Skill a Summonskill*
- (bool) IsCurse
- *Is Skill a Curse*