using System;
namespace net.PeterCashel.DependencyResolver
{
    public class NugetPackageHandler
    {
        public NugetPackageHandler()
        {
        }

        //https://www.nuget.org/api/v2/package/HtmlRenderer.Core/1.5.0.6
        //Nuget (.nupkg) packages via url and loads them into the dep loader


        //It is up to the mod dev to ensure the nupkg url is valid, supports .net 3.5 and that they supply a valid internal path to dlls


        //Steps, DL file, extract with IconicZip, FollowPath, Add libs as files to depResolver
        //lib\net35-client\


    }
}

