using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Spire
{
    // SPBT v1: localized Excel fields have exactly the same int32 storage as ordinary integer fields.
    public sealed class BusinessTable
    {
        public enum FieldType : byte { Int=1, Float=2, Bool=3, String=4 }
        struct Cell { public int Integer; public double Number; public bool Boolean; public string Text; }
        readonly Dictionary<string,int> columns=new Dictionary<string,int>();
        FieldType[] types;
        Cell[][] rows;
        public int RowCount=>rows.Length;
        public int Column(string name)=>columns[name];
        public FieldType Type(int column)=>types[column];
        void Check(int column,FieldType expected) { if(types[column]!=expected) throw new InvalidOperationException("Wrong field type"); }
        public int Int(int row,int column) { Check(column,FieldType.Int); return rows[row][column].Integer; }
        public double Float(int row,int column) { Check(column,FieldType.Float); return rows[row][column].Number; }
        public bool Bool(int row,int column) { Check(column,FieldType.Bool); return rows[row][column].Boolean; }
        public string String(int row,int column) { Check(column,FieldType.String); return rows[row][column].Text; }
        public static BusinessTable Read(byte[] data)
        {
            using(var stream=new MemoryStream(data))
            using(var reader=new BinaryReader(stream))
            {
                if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="SPBT" || reader.ReadInt32()!=1) throw new InvalidDataException("Unsupported business table");
                int fields=reader.ReadInt32(),count=reader.ReadInt32();
                if(fields<1 || fields>256 || count<0 || count>100000 || (long)fields*count>10000000) throw new InvalidDataException("Invalid table counts");
                var table=new BusinessTable { types=new FieldType[fields],rows=new Cell[count][] };
                for(int i=0;i<fields;i++)
                {
                    var name=ReadText(reader); var type=(FieldType)reader.ReadByte();
                    if(name.Length==0 || table.columns.ContainsKey(name) || type<FieldType.Int || type>FieldType.String) throw new InvalidDataException("Invalid table field");
                    table.columns.Add(name,i); table.types[i]=type;
                }
                for(int r=0;r<count;r++)
                {
                    table.rows[r]=new Cell[fields];
                    for(int c=0;c<fields;c++)
                    {
                        switch(table.types[c])
                        {
                            case FieldType.Int: table.rows[r][c].Integer=reader.ReadInt32(); break;
                            case FieldType.Float:
                                double value=reader.ReadDouble();
                                if(double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidDataException("Invalid float");
                                table.rows[r][c].Number=value; break;
                            case FieldType.Bool:
                                byte flag=reader.ReadByte(); if(flag>1) throw new InvalidDataException("Invalid boolean");
                                table.rows[r][c].Boolean=flag==1; break;
                            case FieldType.String: table.rows[r][c].Text=ReadText(reader); break;
                        }
                    }
                }
                if(stream.Position!=stream.Length) throw new InvalidDataException("Trailing table bytes");
                return table;
            }
        }
        static string ReadText(BinaryReader reader)
        {
            int length=reader.ReadInt32(); if(length<0 || length>65536) throw new InvalidDataException("Invalid text length");
            var bytes=reader.ReadBytes(length); if(bytes.Length!=length) throw new EndOfStreamException();
            return new UTF8Encoding(false,true).GetString(bytes);
        }
    }
}
