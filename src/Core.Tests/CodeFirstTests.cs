using System.Threading.Tasks;
using HotChocolate.Execution;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using Moq;
using Xunit;

namespace HotChocolate
{
    public class CodeFirstTests
    {
        [Fact]
        public async Task ExecuteOneFieldQueryWithProperty()
        {
            // arrange
            Schema schema = Schema.Create(
                c => c.RegisterType<QueryTypeWithProperty>());

            // act
            QueryResult result = await schema.ExecuteAsync("{ test }");

            // assert
            Assert.Null(result.Errors);
            Assert.Equal(Snapshot.Current(), Snapshot.New(result));
        }

        [Fact]
        public async Task ExecuteOneFieldQueryWithMethod()
        {
            // arrange
            Schema schema = Schema.Create(
                c => c.RegisterType<QueryTypeWithMethod>());

            // act
            QueryResult result = await schema.ExecuteAsync("{ test }");

            // assert
            Assert.Null(result.Errors);
            Assert.Equal(Snapshot.Current(), Snapshot.New(result));
        }

        [Fact]
        public async Task ExecuteWithUnionType()
        {
            // arrange
            Schema schema = CreateSchema();

            // act
            QueryResult result = await schema.ExecuteAsync(
                "{ fooOrBar { ... on Bar { nameBar } ... on Foo { nameFoo } } }");

            // assert
            Assert.Null(result.Errors);
            Assert.Equal(Snapshot.Current(), Snapshot.New(result));
        }

        [Fact]
        public void UnionTypeResolveType()
        {
            // arrange
            Schema schema = CreateSchema();

            Mock<IResolverContext> context = new Mock<IResolverContext>(
                MockBehavior.Strict);

            // act
            UnionType fooBar = schema.GetType<UnionType>("FooBar");
            ObjectType teaType = fooBar.ResolveType(context.Object, "black_tea");
            ObjectType barType = fooBar.ResolveType(context.Object, "bar");

            // assert
            Assert.Null(teaType);
            Assert.NotNull(barType);
        }

        [Fact]
        public async Task ExecuteWithInterface()
        {
            // arrange
            Schema schema = CreateSchema();

            // act
            QueryResult result = await schema.ExecuteAsync(
                "{ drink { ... on Tea { kind } } }");

            // assert
            Assert.Null(result.Errors);
            Assert.Equal(Snapshot.Current(), Snapshot.New(result));
        }

        [Fact]
        public void InterfaceTypeResolveType()
        {
            // arrange
            Schema schema = CreateSchema();

            Mock<IResolverContext> context = new Mock<IResolverContext>(
                MockBehavior.Strict);

            // act
            InterfaceType drink = schema.GetType<InterfaceType>("Drink");
            ObjectType teaType = drink.ResolveType(context.Object, "black_tea");
            ObjectType barType = drink.ResolveType(context.Object, "bar");

            // assert
            Assert.NotNull(teaType);
            Assert.Null(barType);
        }

        private static Schema CreateSchema()
        {
            return Schema.Create(c =>
            {
                c.RegisterType<QueryType>();
                c.RegisterType<FooType>();
                c.RegisterType<BarType>();
                c.RegisterType<FooBarUnionType>();
                c.RegisterType<DrinkType>();
                c.RegisterType<TeaType>();
            });
        }

        public class QueryTypeWithProperty
            : ObjectType<Query>
        {
            protected override void Configure(IObjectTypeDescriptor<Query> descriptor)
            {
                descriptor.Name("Query");
                descriptor.Field(t => t.TestProp).Name("test");
            }
        }

        public class QueryTypeWithMethod
            : ObjectType<Query>
        {
            protected override void Configure(IObjectTypeDescriptor<Query> descriptor)
            {
                descriptor.Name("Query");
                descriptor.Field(t => t.GetTest()).Name("test");
            }
        }

        public class QueryType
            : ObjectType
        {
            protected override void Configure(IObjectTypeDescriptor descriptor)
            {
                descriptor.Name("Query");
                descriptor.Field("foo")
                    .Type<NonNullType<FooType>>()
                    .Resolver(() => "foo");
                descriptor.Field("bar")
                    .Type<NonNullType<BarType>>()
                    .Resolver(c => "bar");
                descriptor.Field("fooOrBar")
                    .Type<NonNullType<ListType<NonNullType<FooBarUnionType>>>>()
                    .Resolver(() => new object[] { "foo", "bar" });
                descriptor.Field("tea")
                    .Type<TeaType>()
                    .Resolver(() => "black_tea");
                descriptor.Field("drink")
                   .Type<DrinkType>()
                   .Resolver(() => "black_tea");
            }
        }

        public class FooType
            : ObjectType
        {
            protected override void Configure(IObjectTypeDescriptor descriptor)
            {
                descriptor.Name("Foo");
                descriptor.Field("bar")
                    .Type<NonNullType<FooType>>()
                    .Resolver(() => "bar");
                descriptor.Field("nameFoo").Resolver(() => "foo");
                descriptor.IsOfType((c, obj) => obj.Equals("foo"));
            }
        }

        public class BarType
            : ObjectType
        {
            protected override void Configure(IObjectTypeDescriptor descriptor)
            {
                descriptor.Name("Bar");
                descriptor.Field("foo")
                    .Type<NonNullType<FooType>>()
                    .Resolver(() => "foo");
                descriptor.Field("nameBar").Resolver(() => "bar");
                descriptor.IsOfType((c, obj) => obj.Equals("bar"));
            }
        }

        public class TeaType
            : ObjectType
        {
            protected override void Configure(IObjectTypeDescriptor descriptor)
            {
                descriptor.Name("Tea");
                descriptor.Interface<DrinkType>();
                descriptor.Field("kind")
                    .Type<NonNullType<DrinkKindType>>()
                    .Resolver(() => DrinkKind.BlackTea);
                descriptor.IsOfType((c, obj) => obj.Equals("black_tea"));
            }
        }

        public class DrinkType
            : InterfaceType
        {
            protected override void Configure(IInterfaceTypeDescriptor descriptor)
            {
                descriptor.Name("Drink");
                descriptor.Field("kind")
                    .Type<NonNullType<DrinkKindType>>();
            }
        }

        public class DrinkKindType
            : EnumType<DrinkKind>
        {
            protected override void Configure(IEnumTypeDescriptor<DrinkKind> descriptor)
            {
                descriptor.Name("DrinkKind");
            }
        }

        public enum DrinkKind
        {
            BlackTea,
            Water
        }

        public class FooBarUnionType
            : UnionType
        {
            protected override void Configure(IUnionTypeDescriptor descriptor)
            {
                descriptor.Name("FooBar");
                descriptor.Type<BarType>();
                descriptor.Type<FooType>();
            }
        }

    }
}
