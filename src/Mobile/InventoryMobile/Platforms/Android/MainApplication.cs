using Android.App;
using Android.Runtime;

namespace InventoryMobile;

// En Debug, la configuration réseau autorise le HTTP non chiffré vers l'API locale de
// l'émulateur (10.0.2.2) et de la machine. Elle passe par cet attribut et non par un
// manifeste Debug séparé : MAUI impose son propre AndroidManifest après le projet, et
// l'ancien manifeste Debug n'était jamais pris en compte. En Release, rien n'est ajouté :
// l'app n'accepte que le HTTPS.
#if DEBUG
[Application(NetworkSecurityConfig = "@xml/network_security_config")]
#else
[Application]
#endif
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
