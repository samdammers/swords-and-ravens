const webpack = require("webpack");
const HtmlWebpackPlugin = require("html-webpack-plugin");

module.exports = (env, argv) => {
  return {
    output: {
      path: __dirname + "/dist/",
      filename: "bundle.[contenthash].js",
      publicPath: "/static/"
    },
    entry: "./src/client/client.tsx",
    target: "web",
    module: {
      rules: [
        {
          test: /\.tsx?$/,
          use: "ts-loader",
          exclude: /node_modules/
        },
        {
          test: /\.(s*)css$/,
          use: [
            "style-loader",
            "css-loader",
            {
              loader: "sass-loader",
              options: {
                api: "modern",
                sassOptions: {
                  quietDeps: true,
                  silenceDeprecations: [
                    "import",
                    "color-functions",
                    "global-builtin",
                    "slash-div",
                    "abs-percent",
                    "if-function"
                  ]
                }
              }
            }
          ]
        },
        {
          test: /\.(gif|png|jpe?g|svg)$/i,
          use: [
            // Explicit sha256 hash so loader-utils doesn't fall back to its default
            // md4 digest, which OpenSSL 3 (Node.js 17+) refuses to compute.
            {
              loader: "file-loader",
              options: { name: "[sha256:contenthash:hex:16].[ext]" }
            },
            {
              loader: "image-webpack-loader"
            }
          ]
        },
        {
          test: /\.(ogg|mp3|wav|mpe?g)$/i,
          use: {
            loader: "file-loader",
            options: { name: "[sha256:contenthash:hex:16].[ext]" }
          }
        },
        {
          test: /\.(ico)$/i,
          use: {
            loader: "file-loader",
            options: { name: "[sha256:contenthash:hex:16].[ext]" }
          }
        }
      ]
    },
    resolve: {
      extensions: [".tsx", ".ts", ".js"],
      alias: {
        process: "process/browser"
      },
      fallback: {
        crypto: false,
        stream: require.resolve("stream-browserify")
      }
    },
    plugins: [
      new HtmlWebpackPlugin({
        template: "public/index.html"
      }),
      new webpack.EnvironmentPlugin({
        NODE_ENV: argv.mode,
        BUILD_HASH: "devel"
      }),
      new webpack.ProvidePlugin({
        process: "process/browser"
      })
    ],
    // Keep --mode=production behavior (e.g. NODE_ENV) but skip minification so
    // stack traces/sources in devtools stay readable without relying on source maps.
    optimization: {
      minimize: false
    },
    devtool: "source-map",
    devServer: {
      proxy: {
        "/ws": {
          target: "ws://localhost:8000",
          ws: true
        }
      }
    }
  };
};
