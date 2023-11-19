using Shouldly;
using Xunit;

namespace Logicality.EventSourcing.Domain
{
    namespace EventPlayerTests
    {
        public class EventPlayerWithoutHandlers
        {
            private readonly EventPlayer _sut;

            public EventPlayerWithoutHandlers()
            {
                _sut = new EventPlayer();
            }

            [Fact]
            public void RegisterStronglyTypedHandlerHasExpectedResult()
            {
                var @event = new object();

                _sut.Register<object>(played => { @event.ShouldBeSameAs(played); });

                _sut.Play(@event);
            }

            [Fact]
            public void RegisterWeaklyTypedHandlerHasExpectedResult()
            {
                var @event = new object();

                _sut.Register(typeof(object), played => { @event.ShouldBeSameAs(played); });

                _sut.Play(@event);
            }
        }

        public class EventPlayerWithHandlers
        {
            private readonly EventPlayer _sut;

            private readonly object _event;

            public EventPlayerWithHandlers()
            {
                _sut = new EventPlayer();
                _event = new object();
                _sut.Register<object>(played => _event.ShouldBeSameAs(played));
            }

            [Fact]
            public void RegisterStronglyTypedHandlerHasExpectedResult()
            {
                var example = new ExampleEvent();

                _sut.Register<ExampleEvent>(played => { example.ShouldBeSameAs(played); });

                _sut.Play(example);
            }

            [Fact]
            public void RegisterStronglyTypedHandlerForRegisteredEventHasExpectedResult()
            {
                Should.Throw<InvalidOperationException>(() => _sut.Register<object>(played => { }));
            }

            [Fact]
            public void RegisterWeaklyTypedHandlerHasExpectedResult()
            {
                var example = new ExampleEvent();

                _sut.Register(typeof(ExampleEvent), played => { example.ShouldBeSameAs(played); });

                _sut.Play(example);
            }

            [Fact]
            public void RegisterWeaklyTypedHandlerForRegisteredEventHasExpectedResult()
            {
                Should.Throw<InvalidOperationException>(() => _sut.Register(typeof(object), played => { }));
            }

            [Fact]
            public void PlayHasExpectedResult()
            {
                _sut.Play(_event);
            }

            private class ExampleEvent { }
        }
    }
}
