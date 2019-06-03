using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using DBFileReaderLib;
using DBFileReaderLib.Attributes;
using DBDefsLib;

namespace DBCD
{

    public struct DBCDInfo
    {
        internal string tableName;

        internal string[] availableColumns;
    }

    internal class DBCDBuilder
    {
        private ModuleBuilder moduleBuilder;
        private int locStringSize;

        internal DBCDBuilder()
        {
            var assemblyName = new AssemblyName("DBCDDefinitons");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

            this.moduleBuilder = moduleBuilder;
            this.locStringSize = 1;
        }

        internal Tuple<Type, DBCDInfo> Build(DBReader dbcReader, Stream dbd, string name = null, string build = null)
        {
            var dbdReader = new DBDReader();

            if (name == null)
            {
                name = Guid.NewGuid().ToString();
            }

            var databaseDefinition = dbdReader.Read(dbd);

            Structs.VersionDefinitions? versionDefinition = null;

            if (string.IsNullOrWhiteSpace(build) == false)
            {
                var dbBuild = new Build(build);
                locStringSize = GetLocStringSize(dbBuild);
                Utils.GetVersionDefinitionByBuild(databaseDefinition, dbBuild, out versionDefinition);
            }

            if (versionDefinition == null)
            {
                var layoutHash = dbcReader.LayoutHash.ToString("X8");

                Utils.GetVersionDefinitionByLayoutHash(databaseDefinition, layoutHash, out versionDefinition);
            }

            if (versionDefinition == null)
            {
                throw new FileNotFoundException("No definition found for this file.");
            }

            var typeBuilder = moduleBuilder.DefineType(name, TypeAttributes.Public);

            var fields = versionDefinition.Value.definitions;

            foreach (var fieldDefinition in fields)
            {
                var columnDefinition = databaseDefinition.columnDefinitions[fieldDefinition.name];
                bool isLocalisedString = columnDefinition.type == "locstring" && locStringSize > 1;

                var fieldType = FieldDefinitionToType(fieldDefinition, columnDefinition);
                var field = typeBuilder.DefineField(fieldDefinition.name, fieldType, FieldAttributes.Public);

                if (fieldDefinition.isID)
                {
                    AddIndexAttribute(field);
                }

                if(fieldDefinition.arrLength > 1)
                {
                    AddCardinalityAttribute(field, fieldDefinition.arrLength);
                }

                if(isLocalisedString)
                {
                    AddCardinalityAttribute(field, locStringSize);
                    typeBuilder.DefineField(fieldDefinition.name + "_mask", typeof(uint), FieldAttributes.Public);
                }
            }

            var type = typeBuilder.CreateTypeInfo();
            var columns = fields.Select(field => field.name).ToArray();

            var info = new DBCDInfo
            {
                availableColumns = columns,
                tableName = name
            };

            return new Tuple<Type, DBCDInfo>(type, info);
        }

        private int GetLocStringSize(Build build)
        {
            if (build.expansion >= 4)
                return 1;
            if (build.build >= 6692)
                return 16;

            return 8;
        }

        private void AddIndexAttribute(FieldBuilder field)
        {
            var constructorParameters = new Type[] { };
            var constructorInfo = typeof(IndexAttribute).GetConstructor(constructorParameters);
            var displayNameAttributeBuilder = new CustomAttributeBuilder(constructorInfo, new object[] { });
            field.SetCustomAttribute(displayNameAttributeBuilder);
        }

        private void AddCardinalityAttribute(FieldBuilder field, int length)
        {
            var constructorParameters = new Type[] { typeof(int) };
            var constructorInfo = typeof(CardinalityAttribute).GetConstructor(constructorParameters);
            var displayNameAttributeBuilder = new CustomAttributeBuilder(constructorInfo, new object[] { length });
            field.SetCustomAttribute(displayNameAttributeBuilder);
        }

        private Type FieldDefinitionToType(Structs.Definition field, Structs.ColumnDefinition column)
        {
            var isArray = field.arrLength != 0;

            switch (column.type)
            {
                case "int":
                    {
                        Type type = null;
                        var signed = field.isSigned;

                        switch (field.size)
                        {
                            case 8:
                                type = signed ? typeof(sbyte) : typeof(byte);
                                break;
                            case 16:
                                type = signed ? typeof(short) : typeof(ushort);
                                break;
                            case 32:
                                type = signed ? typeof(int) : typeof(uint);
                                break;
                            case 64:
                                type = signed ? typeof(long) : typeof(ulong);
                                break;
                        }

                        return isArray ? type.MakeArrayType() : type;
                    }
                case "string":
                    return isArray ? typeof(string[]) : typeof(string);
                case "locstring":
                    return locStringSize > 1 || isArray ? typeof(string[]) : typeof(string);
                case "float":
                    return isArray ? typeof(float[]) : typeof(float);
                default:
                    throw new ArgumentException("Unable to construct C# type from " + column.type);
            }
        }
    }
}