{
  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-25.05";
  };

  # build with `.?submodules=1`
  outputs = { self, nixpkgs }:
    let
      inherit (nixpkgs) lib;

      makePackages = (pkgs: {
        default = pkgs.stdenv.mkDerivation {
          pname = "MCGalaxy-Relay-Plugin";
          version = "0.0.1-${self.shortRev or self.dirtyShortRev}";

          src = lib.sourceByRegex ./. [
            "^MCGalaxy-Relay-Plugin(/.*)?$"
            "^MCGalaxy-Relay-Plugin\.sln$"
            "^MCGalaxy(/.*)?$"
          ];

          dontConfigureNuget = true;

          nativeBuildInputs = with pkgs; [
            dotnet-sdk_9
            mono
          ];

          FrameworkPathOverride = "${pkgs.mono}/lib/mono/4.7.2-api";

          buildPhase = ''
            dotnet restore
            dotnet build --no-restore --configuration Release
            dotnet test --no-restore --verbosity normal
          '';

          installPhase = ''
            install -Dm 644 \
              ./MCGalaxy-Relay-Plugin/bin/Release/MCGalaxy-Relay-Plugin.dll \
              $out/lib/MCGalaxy-Relay-Plugin.dll
          '';
        };
      });
    in
    builtins.foldl' lib.recursiveUpdate { } (builtins.map
      (system:
        let
          pkgs = import nixpkgs {
            inherit system;
          };
          packages = makePackages pkgs;
        in
        {
          devShells.${system} = packages;
          packages.${system} = packages;
        })
      lib.systems.flakeExposed);
}
