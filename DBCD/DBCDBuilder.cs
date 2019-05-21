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

        internal DBCDBuilder()
        {
            var assemblyName = new AssemblyName("DBCDDefinitons");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

            this.moduleBuilder = moduleBuilder;
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
                Utils.GetVersionDefinitionByBuild(databaseDefinition, new Build(build), out versionDefinition);
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
                var fieldType = FieldDefinitionToType(fieldDefinition, columnDefinition);
                var field = typeBuilder.DefineField(fieldDefinition.name, fieldType, FieldAttributes.Public);

                if (fieldDefinition.isID)
                {
                    var constructorParameters = new Type[] { };
                    var constructorInfo = typeof(IndexAttribute).GetConstructor(constructorParameters);
                    var displayNameAttributeBuilder = new CustomAttributeBuilder(constructorInfo, new object[] { });
                    field.SetCustomAttribute(displayNameAttributeBuilder);
                }

                if(fieldDefinition.arrLength > 1)
                {
                    var constructorParameters = new Type[] { };
                    var constructorInfo = typeof(CardinalityAttribute).GetConstructor(constructorParameters);
                    var displayNameAttributeBuilder = new CustomAttributeBuilder(constructorInfo, new object[] { fieldDefinition.arrLength });
                    field.SetCustomAttribute(displayNameAttributeBuilder);
                }
            }
            var type = typeBuilder.CreateTypeInfo();
            var columns = fields.Select(field => field.name).ToArray();

            var info = new DBCDInfo();
            info.availableColumns = columns;
            info.tableName = name;

            return new Tuple<Type, DBCDInfo>(type, info);
        }

        private static Type FieldDefinitionToType(Structs.Definition field, Structs.ColumnDefinition column)
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
                case "locstring":
                    return isArray ? typeof(string[]) : typeof(string);
                case "float":
                    return isArray ? typeof(float[]) : typeof(float);
                default:
                    throw new ArgumentException("Unable to construct C# type from " + column.type);
            }
        }
    }
}