using Rclsharp.Common;
using Rclsharp.Rcl.Naming;

namespace Rclsharp.Tests.Rcl;

public class TopicNameManglerTests
{
    [Theory]
    [InlineData("chatter", "rt/chatter")]
    [InlineData("/chatter", "rt/chatter")]
    [InlineData("/foo/bar", "rt/foo/bar")]
    public void MangleTopic_は_先頭スラッシュを除いて_rt_を付ける(string input, string expected)
    {
        TopicNameMangler.MangleTopic(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("rt/chatter", "chatter")]
    [InlineData("rt/foo/bar", "foo/bar")]
    [InlineData("not_rt_prefix", "not_rt_prefix")]
    public void DemangleTopic_は_rt_prefix_を除く(string input, string expected)
    {
        TopicNameMangler.DemangleTopic(input).Should().Be(expected);
    }
}

public class TypeNameManglerTests
{
    [Theory]
    [InlineData("std_msgs/msg/String", "std_msgs::msg::dds_::String_")]
    [InlineData("geometry_msgs/msg/Twist", "geometry_msgs::msg::dds_::Twist_")]
    public void MangleType_は_dds_と末尾アンダースコアを付ける(string input, string expected)
    {
        TypeNameMangler.MangleType(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("std_msgs::msg::dds_::String_", "std_msgs/msg/String")]
    [InlineData("geometry_msgs::msg::dds_::Twist_", "geometry_msgs/msg/Twist")]
    public void DemangleType_は_dds_と末尾アンダースコアを除く(string input, string expected)
    {
        TypeNameMangler.DemangleType(input).Should().Be(expected);
    }

    [Fact]
    public void Mangle_と_Demangle_は_往復()
    {
        var ros = "std_msgs/msg/String";
        TypeNameMangler.DemangleType(TypeNameMangler.MangleType(ros)).Should().Be(ros);
    }
}

public class UserEntityIdAllocatorTests
{
    [Fact]
    public void 同じ_topic_名は_常に同じ_writer_id()
    {
        var a = UserEntityIdAllocator.WriterFor("rt/chatter");
        var b = UserEntityIdAllocator.WriterFor("rt/chatter");
        a.Should().Be(b);
        a.Kind.Should().Be(EntityKind.UserDefinedWriterNoKey);
    }

    [Fact]
    public void Reader_は_kind_が_異なる()
    {
        var w = UserEntityIdAllocator.WriterFor("rt/chatter");
        var r = UserEntityIdAllocator.ReaderFor("rt/chatter");
        w.Key.Should().Be(r.Key); // same topic → same key
        w.Kind.Should().Be(EntityKind.UserDefinedWriterNoKey);
        r.Kind.Should().Be(EntityKind.UserDefinedReaderNoKey);
    }

    [Fact]
    public void 異なる_topic_名は_異なる_id()
    {
        var a = UserEntityIdAllocator.WriterFor("rt/foo");
        var b = UserEntityIdAllocator.WriterFor("rt/bar");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Key_は_24bit_に収まる()
    {
        var id = UserEntityIdAllocator.WriterFor("some/very/long/topic/name");
        id.Key.Should().BeLessOrEqualTo(0x00FF_FFFFu);
    }
}
