package com.example.android.trackr.stock

import android.Manifest
import android.app.Activity
import android.content.Intent
import android.content.pm.PackageManager
import android.database.sqlite.SQLiteDatabase
import android.location.Location
import android.location.LocationListener
import android.location.LocationManager
import android.net.Uri
import android.nfc.NfcAdapter
import android.nfc.Tag
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.util.AtomicFile
import android.webkit.JavascriptInterface
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.core.content.FileProvider
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning
import org.json.JSONArray
import org.json.JSONObject
import java.io.ByteArrayInputStream
import java.io.File

/** Shared by the View and Compose applications. Hosts packaged content only. */
class StockWorkspaceActivity : Activity(), NfcAdapter.ReaderCallback {
    private lateinit var web: WebView
    private val handler = Handler(Looper.getMainLooper())
    private var pickerId: String? = null
    private var exportRequest: JSONObject? = null
    private var nfcId: String? = null
    private var locationId: String? = null
    private var locationListener: LocationListener? = null
    private val stateFile by lazy { AtomicFile(File(filesDir,"stock-workspace-v1.json")) }
    private val adapter by lazy { NfcAdapter.getDefaultAdapter(this) }
    private val known = setOf("index.html","styles.css","core.js","app.js","catalog.json")
    private val origin = "https://appassets.androidplatform.net/stock/"

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        title="Stock & deliveries"
        actionBar?.setDisplayHomeAsUpEnabled(true)
        web=WebView(this)
        web.settings.javaScriptEnabled=true
        web.settings.allowFileAccess=false
        web.settings.allowContentAccess=false
        web.settings.domStorageEnabled=false
        web.addJavascriptInterface(Bridge(),"StockNative")
        web.webViewClient=object : WebViewClient() {
            override fun shouldOverrideUrlLoading(view:WebView,request:WebResourceRequest):Boolean = !request.url.toString().startsWith(origin)
            override fun shouldInterceptRequest(view:WebView,request:WebResourceRequest):WebResourceResponse {
                val url=request.url.toString()
                val name=url.removePrefix(origin)
                if(!url.startsWith(origin)||name !in known)
                    return WebResourceResponse("text/plain","utf-8",403,"Blocked",emptyMap(),ByteArrayInputStream(byteArrayOf()))
                val mime=when { name.endsWith(".js")->"application/javascript";name.endsWith(".css")->"text/css";name.endsWith(".json")->"application/json";else->"text/html" }
                return WebResourceResponse(mime,"utf-8",assets.open(name))
            }
        }
        setContentView(web)
        web.loadUrl(origin+"index.html")
    }
    override fun onOptionsItemSelected(item:android.view.MenuItem):Boolean {
        if(item.itemId==android.R.id.home){finish();return true}
        return super.onOptionsItemSelected(item)
    }
    private fun reply(id:String,value:Any?=null,error:String?=null) {
        val response=JSONObject().put("id",id)
        if(error!=null)response.put("error",error) else response.put("value",value?:JSONObject.NULL)
        runOnUiThread { if(!isFinishing)web.evaluateJavascript("window.stockNativeResult("+response.toString()+");",null) }
    }
    inner class Bridge {
        @JavascriptInterface fun postMessage(message:String) {
            var id=""
            try {
                if(message.length>21_000_000)error("Request exceeds 20 MB.")
                val request=JSONObject(message);id=request.getString("id")
                when(request.getString("action")) {
                    "load"->reply(id,load())
                    "save"->reply(id,save(request.getJSONObject("payload")))
                    else->runOnUiThread { action(id,request.getString("action"),request.optJSONObject("payload")) }
                }
            } catch(e:Exception){reply(id,error=e.message?:"Device action failed.")}
        }
    }
    @Synchronized private fun load():JSONObject {
        if(stateFile.baseFile.exists() || File(stateFile.baseFile.path+".bak").exists())return JSONObject(String(stateFile.readFully(),Charsets.UTF_8))
        val items=JSONArray(assets.open("catalog.json").bufferedReader().use{it.readText()})
        val database=getDatabasePath("trackr-db")
        if(database.exists()) {
            SQLiteDatabase.openDatabase(database.path,null,SQLiteDatabase.OPEN_READONLY).use { db ->
                db.rawQuery("SELECT id,name,quantity,category,manifestNumber FROM inventory_items",null).use { cursor ->
                    while(cursor.moveToNext()) {
                        val code=cursor.getString(4).ifBlank { "LEGACY-"+cursor.getLong(0) }
                        items.put(JSONObject().put("sku",code).put("name",cursor.getString(1)).put("category",cursor.getString(3))
                            .put("unit","pcs").put("barcode","").put("openingQuantity",cursor.getInt(2)).put("reorderLevel",0))
                    }
                }
            }
        }
        return JSONObject().put("schema",1).put("revision",0).put("items",items).put("movements",JSONArray())
    }
    @Synchronized private fun save(next:JSONObject):JSONObject {
        val old=load()
        require(next.getInt("schema")==1 && next.getInt("revision")==old.getInt("revision")){"Inventory changed. Reload before saving."}
        require(next.getJSONArray("items").length()<=5000 && next.getJSONArray("movements").length()<=10000){"Inventory is too large."}
        val current=next.getJSONArray("movements")
        val signed=(0 until current.length()).associate { current.getJSONObject(it).getString("id") to current.getJSONObject(it).toString() }
        val previous=old.getJSONArray("movements")
        for(i in 0 until previous.length()) {
            val record=previous.getJSONObject(i)
            require(signed[record.getString("id")]==record.toString()){"Signed records cannot be changed or removed."}
        }
        next.put("revision",old.getInt("revision")+1)
        val bytes=next.toString().toByteArray(Charsets.UTF_8)
        require(bytes.size<=20_000_000){"Workspace exceeds 20 MB."}
        val out=stateFile.startWrite()
        try {out.write(bytes);stateFile.finishWrite(out)}catch(e:Exception){stateFile.failWrite(out);throw e}
        return next
    }
    private fun action(id:String,kind:String,payload:JSONObject?) {
        try {
            when(kind) {
                "scan"->GmsBarcodeScanning.getClient(this).startScan()
                    .addOnSuccessListener { result->val value=result.rawValue;if(value.isNullOrBlank())reply(id,error="No code was read.")else reply(id,value) }
                    .addOnCanceledListener {reply(id,error="Scan cancelled.")}
                    .addOnFailureListener {reply(id,error="Scanner unavailable. Use a reader in keyboard mode or enter the code.")}
                "nfc"->{
                    require(adapter?.isEnabled==true){"NFC is unavailable or switched off."}
                    require(nfcId==null){"An NFC scan is already active."}
                    nfcId=id
                    adapter?.enableReaderMode(this,this,NfcAdapter.FLAG_READER_NFC_A or NfcAdapter.FLAG_READER_NFC_B or NfcAdapter.FLAG_READER_NFC_F or NfcAdapter.FLAG_READER_NFC_V or NfcAdapter.FLAG_READER_SKIP_NDEF_CHECK,null)
                    handler.postDelayed({if(nfcId==id){nfcId=null;adapter?.disableReaderMode(this);reply(id,error="NFC scan timed out.")}},30000)
                }
                "location"->{locationId=id;requestLocation()}
                "import"->{
                    require(pickerId==null){"A file picker is already open."};pickerId=id
                    startActivityForResult(Intent(Intent.ACTION_OPEN_DOCUMENT).apply{type="*/*";addCategory(Intent.CATEGORY_OPENABLE);putExtra(Intent.EXTRA_MIME_TYPES,arrayOf("application/json","text/csv","text/*"))},41)
                }
                "export"->{
                    require(pickerId==null){"A file picker is already open."}
                    val p=payload!!;require(p.getString("contents").length<=20_000_000){"Export exceeds 20 MB."}
                    val name=File(p.getString("name")).name
                    require(name.endsWith(".json")||name.endsWith(".csv")||name.endsWith(".html")){"Unsupported file."}
                    if(p.optBoolean("share")) {
                        val directory=File(cacheDir,"stock-exports").apply{mkdirs()}
                        val file=File(directory,name).apply{writeText(p.getString("contents"))}
                        val uri=FileProvider.getUriForFile(this,packageName+".stockfiles",file)
                        startActivity(Intent.createChooser(Intent(Intent.ACTION_SEND).apply{type=p.getString("type");putExtra(Intent.EXTRA_STREAM,uri);addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)},"Share SiteSync record"))
                        reply(id,true)
                    } else {
                        pickerId=id;exportRequest=p
                        startActivityForResult(Intent(Intent.ACTION_CREATE_DOCUMENT).apply{type=p.getString("type");addCategory(Intent.CATEGORY_OPENABLE);putExtra(Intent.EXTRA_TITLE,name)},42)
                    }
                }
                else->reply(id,error="Unsupported device action.")
            }
        }catch(e:Exception){reply(id,error=e.message?:"Device action failed.")}
    }
    override fun onTagDiscovered(tag:Tag) {
        val id=nfcId?:return;nfcId=null
        val value=tag.id.joinToString("") { "%02X".format(it.toInt() and 255) }
        runOnUiThread {adapter?.disableReaderMode(this);reply(id,value)}
    }
    private fun requestLocation() {
        if(checkSelfPermission(Manifest.permission.ACCESS_FINE_LOCATION)!=PackageManager.PERMISSION_GRANTED && checkSelfPermission(Manifest.permission.ACCESS_COARSE_LOCATION)!=PackageManager.PERMISSION_GRANTED) {
            requestPermissions(arrayOf(Manifest.permission.ACCESS_FINE_LOCATION,Manifest.permission.ACCESS_COARSE_LOCATION),43);return
        }
        val id=locationId?:return
        val manager=getSystemService(LOCATION_SERVICE) as LocationManager
        val provider=when {manager.isProviderEnabled(LocationManager.NETWORK_PROVIDER)->LocationManager.NETWORK_PROVIDER;manager.isProviderEnabled(LocationManager.GPS_PROVIDER)->LocationManager.GPS_PROVIDER;else->{locationId=null;reply(id,error="Location is switched off. Enter the site manually.");return}}
        val listener=object:LocationListener {
            override fun onLocationChanged(location:Location) {
                manager.removeUpdates(this);locationListener=null;locationId=null
                reply(id,JSONObject().put("latitude",location.latitude).put("longitude",location.longitude).put("accuracy",location.accuracy.toDouble()))
            }
            override fun onProviderEnabled(provider:String) {}
            override fun onProviderDisabled(provider:String) {}
            @Deprecated("Deprecated in Android") override fun onStatusChanged(provider:String?,status:Int,extras:Bundle?) {}
        }
        locationListener=listener
        try {manager.requestSingleUpdate(provider,listener,mainLooper)}
        catch(e:SecurityException){locationId=null;reply(id,error="Location permission was not granted.");return}
        handler.postDelayed({if(locationId==id){manager.removeUpdates(listener);locationId=null;locationListener=null;reply(id,error="Location timed out. Enter the site manually.")}},20000)
    }
    override fun onRequestPermissionsResult(requestCode:Int,permissions:Array<out String>,grantResults:IntArray) {
        super.onRequestPermissionsResult(requestCode,permissions,grantResults)
        if(requestCode==43) {
            if(grantResults.any{it==PackageManager.PERMISSION_GRANTED})requestLocation()
            else {locationId?.let{reply(it,error="Location permission was not granted.")};locationId=null}
        }
    }
    @Deprecated("Uses platform document picker for compatibility")
    override fun onActivityResult(requestCode:Int,resultCode:Int,data:Intent?) {
        super.onActivityResult(requestCode,resultCode,data)
        if(requestCode!=41&&requestCode!=42)return
        val id=pickerId?:return;pickerId=null
        try {
            val uri=data?.data
            if(resultCode!=RESULT_OK||uri==null){reply(id,null);exportRequest=null;return}
            if(requestCode==41) {
                val bytes=contentResolver.openInputStream(uri)!!.use{it.readBytesWithLimit(20_000_000)}
                reply(id,String(bytes,Charsets.UTF_8))
            } else {
                val contents=exportRequest!!.getString("contents");exportRequest=null
                contentResolver.openOutputStream(uri,"wt")!!.use{it.write(contents.toByteArray(Charsets.UTF_8))};reply(id,true)
            }
        }catch(e:Exception){exportRequest=null;reply(id,error=e.message?:"File action failed.")}
    }
    private fun java.io.InputStream.readBytesWithLimit(limit:Int):ByteArray {
        val output=java.io.ByteArrayOutputStream();val buffer=ByteArray(8192)
        while(true){val size=read(buffer);if(size<0)break;require(output.size()+size<=limit){"File exceeds 20 MB."};output.write(buffer,0,size)}
        return output.toByteArray()
    }
    override fun onPause() {
        nfcId?.let{reply(it,error="NFC scan stopped.")};nfcId=null;adapter?.disableReaderMode(this)
        if(locationListener!=null) {
            (getSystemService(LOCATION_SERVICE) as LocationManager).removeUpdates(locationListener!!)
            locationListener=null
            locationId?.let{reply(it,error="Location capture stopped.")};locationId=null
        }
        super.onPause()
    }
    override fun onDestroy(){web.removeJavascriptInterface("StockNative");web.destroy();super.onDestroy()}
}
