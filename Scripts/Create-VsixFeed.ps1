param (
    [string] $version,
    [string] $urlPrefix
    )

xml = @"
<?xml version="1.0" encoding="utf-8"?>
<feed xmlns="http://www.w3.org/2005/Atom">
  <title type="text">VSVim GitHub Releases</title>
  <id>uuid:44ca2062-39fc-4bc2-a9c4-2c6ac4469cc4;id=1</id>
  <updated>2025-09-09T09:56:35Z</updated>
  <entry>
    <id>VsVim.Microsoft.e97cd707-324b-4e35-a669-eef8dae4b8cf</id>
    <title type="text">VsVim</title>
    <summary type="text">VsVim</summary>
    <published>2024-06-26T10:55:47-07:00</published>
    <updated>2024-06-26T10:55:13-07:00</updated>
    <author>
      <name>Jared Parsons</name>
    </author>
    <content type="application/octet-stream" src="$($urlPrefix)VisualStudio.GitHub.Copilot.vsix" />
    <Vsix xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns="http://schemas.microsoft.com/developer/vsx-syndication-schema/2010">
      <Id>VisualStudio.GitHub.Copilot</Id>
      <Version>$($version)</Version>
      <Rating xsi:nil="true" />
      <RatingCount xsi:nil="true" />
      <DownloadCount xsi:nil="true" />
      <Installations>
        <Identifier>Microsoft.VisualStudio.Community</Identifier>
        <VersionRange>[17.10,18.0)</VersionRange>
        <ProductArchitecture>amd64</ProductArchitecture>
      </Installations>
      <Installations>
        <Identifier>Microsoft.VisualStudio.Community</Identifier>
        <VersionRange>[17.10,18.0)</VersionRange>
        <ProductArchitecture>arm64</ProductArchitecture>
      </Installations>
    </Vsix>
  </entry>
</feed>
"@