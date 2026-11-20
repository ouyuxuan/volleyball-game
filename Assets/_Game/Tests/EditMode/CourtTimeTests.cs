using System;
using NUnit.Framework;
using Sandspike.Art;
using UnityEngine;

namespace Sandspike.Tests
{
    public class CourtTimeTests
    {
        GameObject lightGo;
        Material sky;
        // RenderSettings before the test, put back in TearDown
        Light oldSun; Material oldSky; Color oldA, oldB, oldC;

        [SetUp]
        public void SetUp()
        {
            oldSun = RenderSettings.sun; oldSky = RenderSettings.skybox;
            oldA = RenderSettings.ambientSkyColor; oldB = RenderSettings.ambientEquatorColor; oldC = RenderSettings.ambientGroundColor;
            CourtTimes.Forget();
            lightGo = new GameObject("Test Sun", typeof(Light));
            var l = lightGo.GetComponent<Light>();
            l.type = LightType.Directional; l.color = Palette.SunLight; l.intensity = 1.3f;
            lightGo.transform.rotation = Quaternion.Euler(40f, 20f, 0f);
            sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_Exposure", 1.25f);
            RenderSettings.sun = l; RenderSettings.skybox = sky;
            RenderSettings.ambientSkyColor = Palette.AmbientSky; RenderSettings.ambientEquatorColor = Palette.AmbientMid; RenderSettings.ambientGroundColor = Palette.AmbientGround;
        }

        [TearDown]
        public void TearDown()
        {
            CourtTimes.Forget();
            RenderSettings.sun = oldSun; RenderSettings.skybox = oldSky;
            RenderSettings.ambientSkyColor = oldA; RenderSettings.ambientEquatorColor = oldB; RenderSettings.ambientGroundColor = oldC;
            ToonGlobals.Apply();
            UnityEngine.Object.DestroyImmediate(lightGo);
            UnityEngine.Object.DestroyImmediate(sky);
        }

        [Test]
        public void TableHasAnEntryForEveryCourtTime() =>
            Assert.AreEqual(Enum.GetValues(typeof(CourtTime)).Length, CourtTimes.Table.Length);

        [Test]
        public void DayRestoresTheRememberedLookExactly()
        {
            Light l = lightGo.GetComponent<Light>();
            Quaternion rot = lightGo.transform.rotation;
            CourtTimes.Apply(CourtTime.Night);
            Assert.AreNotEqual(sky, RenderSettings.skybox, "night uses a copy of the sky");
            Assert.AreEqual(CourtTimes.Table[(int)CourtTime.Night].sunIntensity, l.intensity);
            CourtTimes.Apply(CourtTime.Day);
            Assert.AreEqual(Palette.SunLight, l.color);
            Assert.AreEqual(1.3f, l.intensity);
            Assert.AreEqual(rot, lightGo.transform.rotation);
            Assert.AreEqual(Palette.AmbientSky, RenderSettings.ambientSkyColor);
            Assert.AreEqual(Palette.AmbientMid, RenderSettings.ambientEquatorColor);
            Assert.AreEqual(Palette.AmbientGround, RenderSettings.ambientGroundColor);
            Assert.AreEqual(sky, RenderSettings.skybox);
        }

        [Test]
        public void TheOriginalSkyMaterialIsNeverModified()
        {
            CourtTimes.Apply(CourtTime.Night);
            CourtTimes.Apply(CourtTime.Evening);
            Assert.AreEqual(1.25f, sky.GetFloat("_Exposure"));
        }
    }
}
