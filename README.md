# Discord RPC for SoundCloud
SoundCloud doesn't allow requesting new API keys for developers, and it has been this way for **years** for some unknown reasons. [See this](https://soundcloud.com/you/apps/new)\
So what did I decide to do? I spent WAYYY too much time to make this work and made it unnecessarily advanced.

Don't wanna manually configure it? Sure, just run the **built-in proxy capture server**, that will automatically get the details for you.\
**Wanna manually configure it anyways?** Sure, here are the steps:
1. Go to [SoundCloud](https://soundcloud.com/)
2. Open developer tools in your browser
3. Go to the network tab
4. Play a random song
5. Search for requests with the "me?client_id=" parameters
6. Copy the string after "me?client_id=" and paste it into the SoundCloud RPC
7. Next, go to the headers on the same request, scroll down to the Request Headers section and copy the "Authorization" header (omit the "OAuth " at the beginning)."
8. Paste the Authorization header without the OAuth string into the SoundCloud RPC
9. Everything should be set and ready to go!

This example was made using Firefox, other browsers may differ.\
** PSA: You will need to retrieve these settings every time you log out of SoundCloud, because the tokens refresh. **

# Dependencies used
[Titanium.Web.Proxy](https://github.com/justcoding121/titanium-web-proxy)\
[Costura.Fody](https://github.com/Fody/Costura)\
[Serilog](https://github.com/serilog/serilog)\
[Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)

# License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
