using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

using DB2FileReaderLib.NET;
using DB2FileReaderLib.NET.Attributes;
using DBDefsLib;

namespace DBCD
{
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

        internal Type Build(Stream dbc, Stream dbd, string name = null, string build = null)
        {
            var dbdReader = new DBDReader();
            var dbcReader = ReaderForDBC(dbc);

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
                var type = FieldDefinitionToType(fieldDefinition, columnDefinition);
                var field = typeBuilder.DefineField(fieldDefinition.name, type, FieldAttributes.Public);

                if (fieldDefinition.isID)
                {
                    var constructorParameters = new Type[] { };
                    var constructorInfo = typeof(IndexAttribute).GetConstructor(constructorParameters);
                    var displayNameAttributeBuilder = new CustomAttributeBuilder(constructorInfo, new object[] { });
                    field.SetCustomAttribute(displayNameAttributeBuilder);
                }
            }

            return typeBuilder.CreateTypeInfo();
        }

        private static DB2Reader ReaderForDBC(Stream dbc)
        {
            var reader = new BinaryReader(dbc);
            var identifier = new string(reader.ReadChars(4));

            reader.BaseStream.Position = 0;

            switch (identifier)
            {
                case "WDC3":
                    return new WDC3Reader(dbc);
                case "WDC2":
                    return new WDC2Reader(dbc);
                case "WDC1":
                    return new WDC1Reader(dbc);
                default:
                    throw new ArgumentException("DBC type " + identifier + " is not supported!");
            }
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