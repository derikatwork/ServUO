using System;
using Server;
using Server.Multis;
using Server.Mobiles;
using System.Collections.Generic;
using Server.ContextMenus;

namespace Server.Items
{
    public class MooringLine : Item
    {
        [CommandProperty(AccessLevel.GameMaster)]
        public BaseBoat Boat { get; private set; }

        public override int LabelNumber { get { return 1149697; } } // mooring line

        public MooringLine(BaseBoat boat)
            : base(5368)
        {
            Boat = boat;
            Movable = false;
            Weight = 0;
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (Boat == null || from == null)
                return;

            BaseBoat boat = BaseBoat.FindBoatAt(from, from.Map);

            int range = boat != null && boat == Boat ? 3 : 8;
            bool canMove = false;

            if (Boat != null)
            {
                Boat.Refresh(from);
            }

            if (boat != null && Boat != boat)
            {
                boat.Refresh(from);
            }

            if (!from.InRange(Location, range))
                from.SendLocalizedMessage(500295); //You are too far away to do that.
            else if (!from.InLOS(Location))
                from.SendLocalizedMessage(500950); //You cannot see that.
            else if (Boat.IsMoving || Boat.IsTurning)
                from.SendLocalizedMessage(1116611); //You can't use that while the ship is moving!
            else if (BaseBoat.IsDriving(from))
                from.SendLocalizedMessage(1116610); //You can't do that while piloting a ship!
            else if (BaseHouse.FindHouseAt(from) != null)
                from.SendLocalizedMessage(1149795); //You may not dock a ship while on another ship or inside a house.
            else if (!Boat.IsClassicBoat)
            {
                if (boat == Boat && !MoveToNearestDockOrLand(from))
                    from.SendLocalizedMessage(1149796); //You can not dock a ship this far out to sea. You must be near land or shallow water.
                else if (boat == null)
                {
                    if (!from.Alive)
                        from.SendLocalizedMessage(1060190); //You cannot do that while dead!
                    else if ((Boat is BaseGalleon && ((BaseGalleon)Boat).HasAccess(from)) || (Boat is RowBoat && ((RowBoat)Boat).HasAccess(from)))
                        canMove = true;
                    else
                        from.SendLocalizedMessage(1116617); //You do not have permission to board this ship.
                }
                else if (boat != null && Boat != boat)
                {
                    if (!from.Alive)
                        from.SendLocalizedMessage(1060190); //You cannot do that while dead!
                    else if (Boat is RowBoat && ((RowBoat)Boat).HasAccess(from))
                        canMove = true;
                    else if (boat is RowBoat && Boat is BaseGalleon && ((BaseGalleon)Boat).HasAccess(from))
                        canMove = true;
                    else if (boat is BaseGalleon && Boat is BaseGalleon && ((BaseGalleon)Boat).HasAccess(from))
                        canMove = true;
                    else
                        from.SendLocalizedMessage(1149795); //You may not dock a ship while on another ship or inside a house.
                }
            }

            if (canMove)
            {
                BaseCreature.TeleportPets(from, Location, Map);
                from.MoveToWorld(Location, Map);

                Boat.SendContainerPacket();
            }
        }

        public override void GetContextMenuEntries(Mobile from, List<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, list);

            if (!from.Alive && Boat.Contains(from))
            {
                list.Add(new RemoveContext(from, this));
            }
        }

        public class RemoveContext : ContextMenuEntry
        {
            private MooringLine m_Line;
            private Mobile m_From;

            public RemoveContext(Mobile from, MooringLine line)
                : base(1043331, 3)
            {
                m_Line = line;
                m_From = from;
            }

            public override void OnClick()
            {
                m_Line.OnDoubleClick(m_From);
            }
        }

        public bool MoveToNearestDockOrLand(Mobile from)
        {
            if ((Boat != null && !Boat.Contains(from)) || !ValidateDockOrLand())
                return false;

            Map map = Map;

            if (map == null)
                return false;

            Rectangle2D rec;
            Point3D nearest = Point3D.Zero;
            Point3D p = Point3D.Zero;

            if (Boat is RowBoat)
                rec = new Rectangle2D(Boat.X - 8, Boat.Y - 8, 16, 16);
            else
            {
                switch (Boat.Facing)
                {
                    default:
                    case Direction.North:
                    case Direction.South:
                        if (X < Boat.X)
                        {
                            rec = new Rectangle2D(X - 8, Y - 8, 8, 16);
                        }
                        else
                        {
                            rec = new Rectangle2D(X, Y - 8, 8, 16);
                        }
                        break;
                    case Direction.West:
                    case Direction.East:
                        if (Y < Boat.Y)
                        {
                            rec = new Rectangle2D(X - 8, Y - 8, 16, 8);
                        }
                        else
                        {
                            rec = new Rectangle2D(X - 8, Y, 16, 8);
                        }
                        break;
                }
            }

            for (int x = rec.X; x <= rec.X + rec.Width; x++)
            {
                for (int y = rec.Y; y <= rec.Y + rec.Height; y++)
                {
                    p = new Point3D(x, y, map.GetAverageZ(x, y));

                    if (ValidateTile(from, ref p))
                    {
                        if (nearest == Point3D.Zero || from.GetDistanceToSqrt(p) < from.GetDistanceToSqrt(nearest))
                            nearest = p;
                    }
                }
            }

            if (nearest != Point3D.Zero)
            {
                BaseCreature.TeleportPets(from, nearest, Map);
                from.MoveToWorld(nearest, Map);

                if (Boat != null)
                    Boat.Refresh();

                return true;
            }

            return false;
        }

        public bool ValidateTile(Mobile from, ref Point3D p)
        {
            int x = p.X;
            int y = p.Y;
            int z = p.Z;

            Map map = from.Map;

            if (Spells.SpellHelper.CheckMulti(p, map) || Region.Find(p, map).IsPartOf<Factions.StrongholdRegion>())
                return false;

            StaticTile[] staticTiles = map.Tiles.GetStaticTiles(x, y, true);
            object highest = null;

            //Gets highest tile, which will be used to determine if we can walk on it.
            foreach (StaticTile tile in staticTiles)
            {
                if (highest == null || (highest is StaticTile && tile.Z + tile.Height > ((StaticTile)highest).Z + ((StaticTile)highest).Height))
                    highest = tile;
            }

            if (highest != null && highest is StaticTile)
            {
                StaticTile st = (StaticTile)highest;

                ItemData id = TileData.ItemTable[st.ID & TileData.MaxItemValue];

                if (id.Surface && !id.Impassable)
                {
                    p.Z = st.Z + st.Height;
                    return from.InLOS(p);
                }

            }

            return map.CanFit(x, y, z, 16, false, false);
        }

        public bool ValidateDockOrLand()
        {
            return BaseGalleon.IsNearLandOrDocks(Boat);
        }

        public MooringLine(Serial serial)
            : base(serial)
        {
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write((int)0);

            writer.Write(Boat);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            Boat = reader.ReadItem() as BaseBoat;
        }
    }
}
